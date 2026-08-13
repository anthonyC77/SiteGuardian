namespace SiteGuardian.Api.Models;

/// <summary>
/// État transitoire d'une demande d'autorisation OAuth en cours (entre la
/// redirection vers Kajabi et le retour sur /api/kajabi/callback). Expire
/// après <see cref="TtlMinutes"/> minutes ; nettoyé après usage.
/// </summary>
public class KajabiOAuthState
{
    public const int TtlMinutes = 10;

    /// <summary>Le paramètre OAuth « state », clé primaire (aléatoire, imprévisible).</summary>
    public string State { get; set; } = string.Empty;

    public string CodeVerifier { get; set; } = string.Empty;

    public string AuthorizationEndpoint { get; set; } = string.Empty;

    public string TokenEndpoint { get; set; } = string.Empty;

    public string? ClientId { get; set; }

    public string? ClientSecret { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public bool IsExpired(DateTimeOffset now) => now > CreatedAt.AddMinutes(TtlMinutes);
}
