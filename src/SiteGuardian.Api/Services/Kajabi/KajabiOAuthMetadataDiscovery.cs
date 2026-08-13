using System.Text.Json;
using System.Text.Json.Serialization;

namespace SiteGuardian.Api.Services.Kajabi;

/// <summary>
/// Découverte des endpoints OAuth du serveur MCP Kajabi : d'abord les métadonnées
/// de ressource protégée (RFC 9728) pour trouver le serveur d'autorisation, puis
/// les métadonnées de ce serveur (RFC 8414) pour les endpoints authorize/token/registration.
/// </summary>
public class KajabiOAuthMetadataDiscovery
{
    private readonly HttpClient _http;

    public KajabiOAuthMetadataDiscovery(HttpClient http) => _http = http;

    public async Task<AuthorizationServerMetadata> DiscoverAsync(Uri mcpServerUrl, CancellationToken ct = default)
    {
        var origin = new Uri(mcpServerUrl.GetLeftPart(UriPartial.Authority));
        var authServerOrigin = await TryDiscoverAuthorizationServerAsync(origin, mcpServerUrl, ct) ?? origin;

        var metadataUrl = new Uri(authServerOrigin, "/.well-known/oauth-authorization-server");
        using var response = await _http.GetAsync(metadataUrl, ct);
        response.EnsureSuccessStatusCode();

        var metadata = await response.Content.ReadFromJsonAsync<AuthorizationServerMetadataDto>(
            JsonOptions, ct)
            ?? throw new InvalidOperationException("Métadonnées du serveur d'autorisation invalides.");

        if (string.IsNullOrEmpty(metadata.AuthorizationEndpoint) || string.IsNullOrEmpty(metadata.TokenEndpoint))
            throw new InvalidOperationException(
                "Le serveur d'autorisation Kajabi n'expose pas authorization_endpoint/token_endpoint.");

        return new AuthorizationServerMetadata(
            metadata.AuthorizationEndpoint, metadata.TokenEndpoint, metadata.RegistrationEndpoint);
    }

    /// <summary>Cherche les métadonnées de ressource protégée (RFC 9728) ; null si absentes (serveur = AS).</summary>
    private async Task<Uri?> TryDiscoverAuthorizationServerAsync(Uri origin, Uri mcpServerUrl, CancellationToken ct)
    {
        var candidates = new[]
        {
            new Uri(origin, "/.well-known/oauth-protected-resource" + mcpServerUrl.AbsolutePath),
            new Uri(origin, "/.well-known/oauth-protected-resource"),
        };

        foreach (var candidate in candidates)
        {
            HttpResponseMessage response;
            try
            {
                response = await _http.GetAsync(candidate, ct);
            }
            catch (HttpRequestException)
            {
                continue;
            }

            using (response)
            {
                if (!response.IsSuccessStatusCode) continue;

                var resource = await response.Content.ReadFromJsonAsync<ProtectedResourceMetadataDto>(
                    JsonOptions, ct);
                var authServer = resource?.AuthorizationServers?.FirstOrDefault();
                if (!string.IsNullOrEmpty(authServer) && Uri.TryCreate(authServer, UriKind.Absolute, out var uri))
                    return uri;
            }
        }

        return null;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private class ProtectedResourceMetadataDto
    {
        [JsonPropertyName("authorization_servers")]
        public List<string>? AuthorizationServers { get; set; }
    }

    private class AuthorizationServerMetadataDto
    {
        [JsonPropertyName("authorization_endpoint")]
        public string? AuthorizationEndpoint { get; set; }

        [JsonPropertyName("token_endpoint")]
        public string? TokenEndpoint { get; set; }

        [JsonPropertyName("registration_endpoint")]
        public string? RegistrationEndpoint { get; set; }
    }
}

public record AuthorizationServerMetadata(
    string AuthorizationEndpoint, string TokenEndpoint, string? RegistrationEndpoint);
