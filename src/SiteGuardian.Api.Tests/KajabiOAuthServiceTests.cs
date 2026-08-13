using System.Net;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SiteGuardian.Api.Data;
using SiteGuardian.Api.Models;
using SiteGuardian.Api.Services.Kajabi;
using SiteGuardian.Api.Tests.Fakes;

namespace SiteGuardian.Api.Tests;

public class KajabiOAuthServiceTests : IDisposable
{
    private readonly SiteGuardianDbContext _db;
    private readonly IDataProtectionProvider _dataProtection;
    private readonly string _keyDir;

    public KajabiOAuthServiceTests()
    {
        var options = new DbContextOptionsBuilder<SiteGuardianDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        _db = new SiteGuardianDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        _keyDir = Path.Combine(Path.GetTempPath(), "sg-dp-oauth-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_keyDir);
        _dataProtection = DataProtectionProvider.Create(new DirectoryInfo(_keyDir));
    }

    public void Dispose()
    {
        _db.Dispose();
        try { Directory.Delete(_keyDir, recursive: true); } catch (IOException) { }
    }

    private static IConfiguration Config(string? clientId = "configured-client") =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Kajabi:McpUrl"] = "https://mcp.kajabi.com/mcp",
            ["Kajabi:Scopes"] = "read write",
            ["Kajabi:ClientId"] = clientId,
        }).Build();

    private KajabiOAuthService CreateService(FakeHttpMessageHandler handler, IConfiguration? config = null)
    {
        var httpClient = handler.ToHttpClient();
        var discovery = new KajabiOAuthMetadataDiscovery(httpClient);
        var registrar = new KajabiDynamicClientRegistrar(httpClient);
        var tokenStore = new KajabiTokenStore(_db, _dataProtection);
        return new KajabiOAuthService(_db, discovery, registrar, tokenStore, httpClient, config ?? Config());
    }

    private KajabiTokenStore TokenStore() => new(_db, _dataProtection);

    private const string AuthServerMetadataJson = """
        {
          "authorization_endpoint": "https://auth.kajabi.com/authorize",
          "token_endpoint": "https://auth.kajabi.com/token"
        }
        """;

    [Fact]
    public async Task BuildAuthorizationUrlAsync_ProducesPkceUrl_AndPersistsState()
    {
        var handler = FakeHttpMessageHandler.MapJson(new()
        {
            ["https://mcp.kajabi.com/.well-known/oauth-authorization-server"] = (HttpStatusCode.OK, AuthServerMetadataJson),
        });
        var service = CreateService(handler);

        var url = await service.BuildAuthorizationUrlAsync(new Uri("https://localhost:5215/api/kajabi/callback"));

        Assert.StartsWith("https://auth.kajabi.com/authorize", url.ToString());
        Assert.Contains("code_challenge_method=S256", url.ToString());
        Assert.Contains("client_id=configured-client", url.ToString());
        Assert.Single(_db.KajabiOAuthStates);

        // AbsoluteUri (forme réellement envoyée sur le fil) doit avoir "scope=read%20write",
        // jamais un espace brut — Uri.ToString() décode et masquerait une régression ici.
        Assert.Contains("scope=read%20write", url.AbsoluteUri);
        Assert.DoesNotContain("read write", url.AbsoluteUri);
    }

    [Fact]
    public async Task BuildAuthorizationUrlAsync_NoClientIdAndNoRegistration_Throws()
    {
        var handler = FakeHttpMessageHandler.MapJson(new()
        {
            ["https://mcp.kajabi.com/.well-known/oauth-authorization-server"] = (HttpStatusCode.OK, AuthServerMetadataJson),
        });
        var service = CreateService(handler, Config(clientId: null));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.BuildAuthorizationUrlAsync(new Uri("https://localhost/api/kajabi/callback")));
    }

    [Fact]
    public async Task HandleCallbackAsync_UnknownState_Throws()
    {
        var handler = FakeHttpMessageHandler.MapJson(new());
        var service = CreateService(handler);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.HandleCallbackAsync("code", "unknown-state", new Uri("https://localhost/api/kajabi/callback")));
    }

    [Fact]
    public async Task HandleCallbackAsync_ValidState_ExchangesCodeAndSavesTokens()
    {
        _db.KajabiOAuthStates.Add(new KajabiOAuthState
        {
            State = "state-1",
            CodeVerifier = "verifier-1",
            AuthorizationEndpoint = "https://auth.kajabi.com/authorize",
            TokenEndpoint = "https://auth.kajabi.com/token",
            ClientId = "configured-client",
        });
        await _db.SaveChangesAsync();

        var handler = FakeHttpMessageHandler.MapJson(new()
        {
            ["https://auth.kajabi.com/token"] = (HttpStatusCode.OK, """
                { "access_token": "at-xyz", "refresh_token": "rt-xyz", "expires_in": 3600, "scope": "read write" }
                """),
        });
        var service = CreateService(handler);

        await service.HandleCallbackAsync("auth-code", "state-1", new Uri("https://localhost/api/kajabi/callback"));

        var connection = await TokenStore().GetConnectionAsync();
        Assert.NotNull(connection);
        Assert.Equal("at-xyz", TokenStore().DecryptAccessToken(connection!));
        Assert.Empty(_db.KajabiOAuthStates); // état à usage unique, consommé
    }

    [Fact]
    public async Task GetValidAccessTokenAsync_NoConnection_ThrowsReconnectRequired()
    {
        var service = CreateService(FakeHttpMessageHandler.MapJson(new()));

        await Assert.ThrowsAsync<KajabiReconnectRequiredException>(() => service.GetValidAccessTokenAsync());
    }

    [Fact]
    public async Task GetValidAccessTokenAsync_StillValid_ReturnsWithoutHttpCall()
    {
        await TokenStore().SaveAsync(
            siteId: "s", accessToken: "still-valid-token", refreshToken: "rt",
            expiresAt: DateTimeOffset.UtcNow.AddHours(1), scopes: "read",
            tokenEndpoint: "https://auth.kajabi.com/token", clientId: "c", clientSecret: null);

        // Le handler jette si appelé : prouve qu'aucune requête HTTP n'est faite.
        var handler = new FakeHttpMessageHandler(_ => throw new InvalidOperationException("Ne devrait pas être appelé"));
        var service = CreateService(handler);

        var token = await service.GetValidAccessTokenAsync();

        Assert.Equal("still-valid-token", token);
    }

    [Fact]
    public async Task GetValidAccessTokenAsync_Expired_RefreshesAndSavesNewToken()
    {
        await TokenStore().SaveAsync(
            siteId: "s", accessToken: "old-token", refreshToken: "refresh-me",
            expiresAt: DateTimeOffset.UtcNow.AddMinutes(-5), scopes: "read",
            tokenEndpoint: "https://auth.kajabi.com/token", clientId: "c", clientSecret: null);

        var handler = FakeHttpMessageHandler.MapJson(new()
        {
            ["https://auth.kajabi.com/token"] = (HttpStatusCode.OK, """
                { "access_token": "new-token", "expires_in": 3600 }
                """),
        });
        var service = CreateService(handler);

        var token = await service.GetValidAccessTokenAsync();

        Assert.Equal("new-token", token);
        var connection = await TokenStore().GetConnectionAsync();
        // Le refresh token n'est pas re-renvoyé par le serveur : l'ancien doit être conservé.
        Assert.Equal("refresh-me", TokenStore().DecryptRefreshToken(connection!));
    }

    [Fact]
    public async Task GetValidAccessTokenAsync_ExpiredNoRefreshToken_ThrowsReconnectRequired()
    {
        await TokenStore().SaveAsync(
            siteId: "s", accessToken: "old-token", refreshToken: null,
            expiresAt: DateTimeOffset.UtcNow.AddMinutes(-5), scopes: "read",
            tokenEndpoint: "https://auth.kajabi.com/token", clientId: "c", clientSecret: null);

        var service = CreateService(new FakeHttpMessageHandler(_ => throw new InvalidOperationException("unexpected")));

        await Assert.ThrowsAsync<KajabiReconnectRequiredException>(() => service.GetValidAccessTokenAsync());
    }
}
