using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using SiteGuardian.Api.Data;
using SiteGuardian.Api.Models;

namespace SiteGuardian.Api.Services.Kajabi;

/// <summary>
/// Lit/écrit la connexion Kajabi avec tokens chiffrés (DataProtection API, §6 du plan).
/// Les tokens en clair ne transitent jamais en dehors de cette classe et de l'appelant immédiat.
/// </summary>
public class KajabiTokenStore
{
    private const string Purpose = "SiteGuardian.KajabiTokens";

    private readonly SiteGuardianDbContext _db;
    private readonly IDataProtector _protector;

    public KajabiTokenStore(SiteGuardianDbContext db, IDataProtectionProvider dataProtection)
    {
        _db = db;
        _protector = dataProtection.CreateProtector(Purpose);
    }

    public Task<KajabiConnection?> GetConnectionAsync(CancellationToken ct = default) =>
        _db.KajabiConnections.FirstOrDefaultAsync(c => c.Id == KajabiConnection.SingletonId, ct);

    public string DecryptAccessToken(KajabiConnection connection) => _protector.Unprotect(connection.EncryptedAccessToken);

    public string? DecryptRefreshToken(KajabiConnection connection) =>
        connection.EncryptedRefreshToken is null ? null : _protector.Unprotect(connection.EncryptedRefreshToken);

    public string? DecryptClientSecret(KajabiConnection connection) =>
        connection.EncryptedClientSecret is null ? null : _protector.Unprotect(connection.EncryptedClientSecret);

    /// <summary>Enregistre (ou remplace) la connexion singleton avec les tokens chiffrés.</summary>
    public async Task SaveAsync(
        string? siteId, string accessToken, string? refreshToken, DateTimeOffset expiresAt,
        string scopes, string tokenEndpoint, string? clientId, string? clientSecret,
        CancellationToken ct = default)
    {
        var existing = await GetConnectionAsync(ct);
        var connection = existing ?? new KajabiConnection();

        connection.SiteId = siteId ?? connection.SiteId;
        connection.EncryptedAccessToken = _protector.Protect(accessToken);
        connection.EncryptedRefreshToken = refreshToken is null ? null : _protector.Protect(refreshToken);
        connection.AccessTokenExpiresAt = expiresAt;
        connection.Scopes = scopes;
        connection.TokenEndpoint = tokenEndpoint;
        connection.ClientId = clientId;
        connection.EncryptedClientSecret = clientSecret is null ? null : _protector.Protect(clientSecret);
        connection.LastRefreshedAt = DateTimeOffset.UtcNow;

        if (existing is null)
        {
            connection.ConnectedAt = DateTimeOffset.UtcNow;
            _db.KajabiConnections.Add(connection);
        }

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Met à jour le site_id (premier appel MCP après connexion, §6) sans toucher aux tokens.</summary>
    public async Task UpdateSiteIdAsync(string siteId, CancellationToken ct = default)
    {
        var connection = await GetConnectionAsync(ct);
        if (connection is null) return;
        connection.SiteId = siteId;
        await _db.SaveChangesAsync(ct);
    }

    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        var connection = await GetConnectionAsync(ct);
        if (connection is not null)
        {
            _db.KajabiConnections.Remove(connection);
            await _db.SaveChangesAsync(ct);
        }
    }
}
