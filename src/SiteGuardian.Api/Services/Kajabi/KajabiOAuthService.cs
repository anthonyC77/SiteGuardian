using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using SiteGuardian.Api.Data;
using SiteGuardian.Api.Models;

namespace SiteGuardian.Api.Services.Kajabi;

/// <summary>Levée quand la connexion Kajabi a besoin d'une reconnexion manuelle (§6 : « état reconnexion requise »).</summary>
public class KajabiReconnectRequiredException : Exception
{
    public KajabiReconnectRequiredException(string message) : base(message) { }
}

/// <summary>
/// Orchestre le flux OAuth 2.1 avec le serveur MCP Kajabi : construction de l'URL
/// d'autorisation (PKCE + découverte + DCR), échange du code, rafraîchissement.
/// </summary>
public class KajabiOAuthService
{
    private readonly SiteGuardianDbContext _db;
    private readonly KajabiOAuthMetadataDiscovery _discovery;
    private readonly KajabiDynamicClientRegistrar _registrar;
    private readonly KajabiTokenStore _tokenStore;
    private readonly HttpClient _http;
    private readonly IConfiguration _config;

    public KajabiOAuthService(
        SiteGuardianDbContext db, KajabiOAuthMetadataDiscovery discovery,
        KajabiDynamicClientRegistrar registrar, KajabiTokenStore tokenStore,
        HttpClient http, IConfiguration config)
    {
        _db = db;
        _discovery = discovery;
        _registrar = registrar;
        _tokenStore = tokenStore;
        _http = http;
        _config = config;
    }

    private Uri McpServerUrl => new(_config["Kajabi:McpUrl"] ?? "https://mcp.kajabi.com/mcp");

    private string Scopes => _config["Kajabi:Scopes"] ?? "read write";

    /// <summary>Construit l'URL vers laquelle rediriger le gestionnaire du site pour démarrer l'autorisation.</summary>
    public async Task<Uri> BuildAuthorizationUrlAsync(Uri redirectUri, CancellationToken ct = default)
    {
        var metadata = await _discovery.DiscoverAsync(McpServerUrl, ct);

        var clientId = _config["Kajabi:ClientId"];
        var clientSecret = _config["Kajabi:ClientSecret"];
        if (string.IsNullOrEmpty(clientId) && !string.IsNullOrEmpty(metadata.RegistrationEndpoint))
        {
            var registered = await _registrar.RegisterAsync(metadata.RegistrationEndpoint, redirectUri, ct);
            clientId = registered.ClientId;
            clientSecret = registered.ClientSecret;
        }
        if (string.IsNullOrEmpty(clientId))
            throw new InvalidOperationException(
                "Aucun client_id Kajabi : configurez Kajabi:ClientId ou activez l'enregistrement dynamique côté serveur.");

        var verifier = PkceGenerator.CreateCodeVerifier();
        var state = PkceGenerator.CreateState();

        await PurgeExpiredStatesAsync(ct);
        _db.KajabiOAuthStates.Add(new KajabiOAuthState
        {
            State = state,
            CodeVerifier = verifier,
            AuthorizationEndpoint = metadata.AuthorizationEndpoint,
            TokenEndpoint = metadata.TokenEndpoint,
            ClientId = clientId,
            ClientSecret = clientSecret,
        });
        await _db.SaveChangesAsync(ct);

        // Construction manuelle (Uri.EscapeDataString) plutôt que QueryHelpers.AddQueryString,
        // qui laisse passer l'espace du paramètre scope non encodé (0x20 brut au lieu de %20).
        var query = new Dictionary<string, string?>
        {
            ["response_type"] = "code",
            ["client_id"] = clientId,
            ["redirect_uri"] = redirectUri.ToString(),
            ["code_challenge"] = PkceGenerator.CreateCodeChallenge(verifier),
            ["code_challenge_method"] = "S256",
            ["state"] = state,
            ["scope"] = Scopes,
        };
        var separator = metadata.AuthorizationEndpoint.Contains('?') ? '&' : '?';
        var queryString = string.Join('&', query
            .Where(kv => kv.Value is not null)
            .Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value!)}"));
        return new Uri($"{metadata.AuthorizationEndpoint}{separator}{queryString}");
    }

    /// <summary>Traite le retour de Kajabi : échange le code contre des tokens et les persiste (chiffrés).</summary>
    public async Task HandleCallbackAsync(string code, string state, Uri redirectUri, CancellationToken ct = default)
    {
        var pending = await _db.KajabiOAuthStates.FindAsync([state], ct)
            ?? throw new InvalidOperationException("État OAuth inconnu ou déjà consommé.");

        _db.KajabiOAuthStates.Remove(pending);
        await _db.SaveChangesAsync(ct);

        if (pending.IsExpired(DateTimeOffset.UtcNow))
            throw new InvalidOperationException("La demande d'autorisation a expiré — relancez la connexion.");

        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = redirectUri.ToString(),
            ["client_id"] = pending.ClientId ?? string.Empty,
            ["code_verifier"] = pending.CodeVerifier,
        };
        if (!string.IsNullOrEmpty(pending.ClientSecret))
            form["client_secret"] = pending.ClientSecret;

        var token = await ExchangeAsync(pending.TokenEndpoint, form, ct);

        await _tokenStore.SaveAsync(
            siteId: null,
            accessToken: token.AccessToken,
            refreshToken: token.RefreshToken,
            expiresAt: DateTimeOffset.UtcNow.AddSeconds(token.ExpiresIn ?? 3600),
            scopes: token.Scope ?? Scopes,
            tokenEndpoint: pending.TokenEndpoint,
            clientId: pending.ClientId,
            clientSecret: pending.ClientSecret,
            ct: ct);
    }

    /// <summary>
    /// Retourne un access token valide, en le rafraîchissant si besoin.
    /// Lève <see cref="KajabiReconnectRequiredException"/> si aucune reconnexion automatique n'est possible.
    /// </summary>
    public async Task<string> GetValidAccessTokenAsync(CancellationToken ct = default)
    {
        var connection = await _tokenStore.GetConnectionAsync(ct)
            ?? throw new KajabiReconnectRequiredException("Aucune connexion Kajabi active.");

        var now = DateTimeOffset.UtcNow;
        if (connection.AccessTokenExpiresAt > now.AddMinutes(1))
            return _tokenStore.DecryptAccessToken(connection);

        var refreshToken = _tokenStore.DecryptRefreshToken(connection)
            ?? throw new KajabiReconnectRequiredException("Token expiré et aucun refresh token disponible.");

        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
            ["client_id"] = connection.ClientId ?? string.Empty,
        };
        var clientSecret = _tokenStore.DecryptClientSecret(connection);
        if (!string.IsNullOrEmpty(clientSecret))
            form["client_secret"] = clientSecret;

        TokenResponse token;
        try
        {
            token = await ExchangeAsync(connection.TokenEndpoint, form, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new KajabiReconnectRequiredException($"Échec du rafraîchissement du token : {ex.Message}");
        }

        await _tokenStore.SaveAsync(
            siteId: connection.SiteId,
            accessToken: token.AccessToken,
            refreshToken: token.RefreshToken ?? refreshToken, // certains serveurs ne renvoient pas de nouveau refresh token
            expiresAt: now.AddSeconds(token.ExpiresIn ?? 3600),
            scopes: token.Scope ?? connection.Scopes,
            tokenEndpoint: connection.TokenEndpoint,
            clientId: connection.ClientId,
            clientSecret: clientSecret,
            ct: ct);

        return token.AccessToken;
    }

    private async Task<TokenResponse> ExchangeAsync(
        string tokenEndpoint, Dictionary<string, string> form, CancellationToken ct)
    {
        using var response = await _http.PostAsync(tokenEndpoint, new FormUrlEncodedContent(form), ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TokenResponse>(ct)
            ?? throw new InvalidOperationException("Réponse du serveur de token Kajabi invalide.");
    }

    private async Task PurgeExpiredStatesAsync(CancellationToken ct)
    {
        // Filtre en mémoire : le provider Sqlite d'EF Core ne traduit pas les comparaisons
        // DateTimeOffset en SQL, et le volume de lignes (états OAuth en attente) reste minuscule.
        var cutoff = DateTimeOffset.UtcNow.AddMinutes(-KajabiOAuthState.TtlMinutes);
        var all = await _db.KajabiOAuthStates.ToListAsync(ct);
        var expired = all.Where(s => s.CreatedAt < cutoff).ToList();
        if (expired.Count > 0)
            _db.KajabiOAuthStates.RemoveRange(expired);
    }

    private class TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; set; }

        [JsonPropertyName("expires_in")]
        public int? ExpiresIn { get; set; }

        [JsonPropertyName("scope")]
        public string? Scope { get; set; }
    }
}
