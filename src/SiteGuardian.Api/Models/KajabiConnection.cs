namespace SiteGuardian.Api.Models;

/// <summary>
/// Connexion OAuth 2.1 au serveur MCP Kajabi (§6 du plan). Ligne singleton
/// (un seul gestionnaire de site utilise l'appli) : Id fixe = 1.
/// Access/refresh tokens chiffrés via l'API DataProtection, jamais en clair.
/// </summary>
public class KajabiConnection
{
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;

    public string? SiteId { get; set; }

    public string EncryptedAccessToken { get; set; } = string.Empty;

    public string? EncryptedRefreshToken { get; set; }

    public DateTimeOffset AccessTokenExpiresAt { get; set; }

    public string Scopes { get; set; } = string.Empty;

    /// <summary>Endpoints découverts (RFC 8414) — mémorisés pour ne pas rejouer la découverte à chaque refresh.</summary>
    public string TokenEndpoint { get; set; } = string.Empty;

    public string? ClientId { get; set; }

    public string? EncryptedClientSecret { get; set; }

    public DateTimeOffset ConnectedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? LastRefreshedAt { get; set; }

    /// <summary>Reconnexion requise (§6) : token expiré et pas de refresh token utilisable.</summary>
    public bool NeedsReconnect(DateTimeOffset now) =>
        AccessTokenExpiresAt <= now && string.IsNullOrEmpty(EncryptedRefreshToken);
}
