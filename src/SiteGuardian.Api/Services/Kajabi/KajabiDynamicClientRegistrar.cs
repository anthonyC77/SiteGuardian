using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace SiteGuardian.Api.Services.Kajabi;

/// <summary>Enregistrement dynamique de client OAuth (RFC 7591) : évite d'exiger un client_id pré-configuré.</summary>
public class KajabiDynamicClientRegistrar
{
    private readonly HttpClient _http;

    public KajabiDynamicClientRegistrar(HttpClient http) => _http = http;

    public async Task<RegisteredClient> RegisterAsync(
        string registrationEndpoint, Uri redirectUri, CancellationToken ct = default)
    {
        var request = new DynamicClientRegistrationRequest
        {
            ClientName = "SiteGuardian",
            RedirectUris = [redirectUri.ToString()],
            GrantTypes = ["authorization_code", "refresh_token"],
            ResponseTypes = ["code"],
            TokenEndpointAuthMethod = "none", // client public : PKCE, pas de secret
        };

        using var response = await _http.PostAsJsonAsync(registrationEndpoint, request, ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<DynamicClientRegistrationResponse>(ct)
            ?? throw new InvalidOperationException("Réponse d'enregistrement de client invalide.");

        if (string.IsNullOrEmpty(result.ClientId))
            throw new InvalidOperationException("Le serveur Kajabi n'a pas renvoyé de client_id.");

        return new RegisteredClient(result.ClientId, result.ClientSecret);
    }

    private class DynamicClientRegistrationRequest
    {
        [JsonPropertyName("client_name")]
        public string ClientName { get; set; } = string.Empty;

        [JsonPropertyName("redirect_uris")]
        public List<string> RedirectUris { get; set; } = [];

        [JsonPropertyName("grant_types")]
        public List<string> GrantTypes { get; set; } = [];

        [JsonPropertyName("response_types")]
        public List<string> ResponseTypes { get; set; } = [];

        [JsonPropertyName("token_endpoint_auth_method")]
        public string TokenEndpointAuthMethod { get; set; } = string.Empty;
    }

    private class DynamicClientRegistrationResponse
    {
        [JsonPropertyName("client_id")]
        public string? ClientId { get; set; }

        [JsonPropertyName("client_secret")]
        public string? ClientSecret { get; set; }
    }
}

public record RegisteredClient(string ClientId, string? ClientSecret);
