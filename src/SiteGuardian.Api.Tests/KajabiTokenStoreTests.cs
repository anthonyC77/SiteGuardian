using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using SiteGuardian.Api.Data;
using SiteGuardian.Api.Models;
using SiteGuardian.Api.Services.Kajabi;

namespace SiteGuardian.Api.Tests;

public class KajabiTokenStoreTests : IDisposable
{
    private readonly SiteGuardianDbContext _db;
    private readonly IDataProtectionProvider _dataProtection;
    private readonly string _keyDir;

    public KajabiTokenStoreTests()
    {
        var options = new DbContextOptionsBuilder<SiteGuardianDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        _db = new SiteGuardianDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        _keyDir = Path.Combine(Path.GetTempPath(), "sg-dp-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_keyDir);
        _dataProtection = DataProtectionProvider.Create(new DirectoryInfo(_keyDir));
    }

    public void Dispose()
    {
        _db.Dispose();
        try { Directory.Delete(_keyDir, recursive: true); } catch (IOException) { }
    }

    private KajabiTokenStore CreateStore() => new(_db, _dataProtection);

    [Fact]
    public async Task SaveAsync_RoundTripsAccessAndRefreshTokens()
    {
        var store = CreateStore();

        await store.SaveAsync(
            siteId: "site_123", accessToken: "at-secret", refreshToken: "rt-secret",
            expiresAt: DateTimeOffset.UtcNow.AddHours(1), scopes: "read write",
            tokenEndpoint: "https://auth.kajabi.com/token", clientId: "client-abc", clientSecret: null);

        var connection = await store.GetConnectionAsync();
        Assert.NotNull(connection);
        Assert.Equal("site_123", connection!.SiteId);
        Assert.Equal("at-secret", store.DecryptAccessToken(connection));
        Assert.Equal("rt-secret", store.DecryptRefreshToken(connection));
    }

    [Fact]
    public async Task SaveAsync_StoresTokensEncrypted_NotPlaintext()
    {
        var store = CreateStore();
        await store.SaveAsync(
            siteId: null, accessToken: "super-secret-token", refreshToken: null,
            expiresAt: DateTimeOffset.UtcNow.AddHours(1), scopes: "read",
            tokenEndpoint: "https://auth.kajabi.com/token", clientId: null, clientSecret: null);

        var connection = await store.GetConnectionAsync();
        Assert.DoesNotContain("super-secret-token", connection!.EncryptedAccessToken);
    }

    [Fact]
    public async Task UpdateSiteIdAsync_ChangesOnlySiteId()
    {
        var store = CreateStore();
        await store.SaveAsync(
            siteId: null, accessToken: "at", refreshToken: "rt",
            expiresAt: DateTimeOffset.UtcNow.AddHours(1), scopes: "read",
            tokenEndpoint: "https://auth.kajabi.com/token", clientId: "c", clientSecret: null);

        await store.UpdateSiteIdAsync("site_789");

        var connection = await store.GetConnectionAsync();
        Assert.Equal("site_789", connection!.SiteId);
        Assert.Equal("at", store.DecryptAccessToken(connection));
    }

    [Fact]
    public async Task DisconnectAsync_RemovesConnection()
    {
        var store = CreateStore();
        await store.SaveAsync(
            siteId: "s", accessToken: "at", refreshToken: null,
            expiresAt: DateTimeOffset.UtcNow.AddHours(1), scopes: "read",
            tokenEndpoint: "https://auth.kajabi.com/token", clientId: null, clientSecret: null);

        await store.DisconnectAsync();

        Assert.Null(await store.GetConnectionAsync());
    }

    [Fact]
    public async Task GetConnectionAsync_NoConnection_ReturnsNull()
    {
        Assert.Null(await CreateStore().GetConnectionAsync());
    }

    [Theory]
    [InlineData(-1, false, false)] // expiré, avec refresh token -> pas de reconnexion forcée
    [InlineData(-1, true, true)]   // expiré, sans refresh token -> reconnexion requise
    [InlineData(1, true, false)]   // encore valide -> pas de reconnexion
    public void NeedsReconnect_MatchesExpiryAndRefreshTokenPresence(
        int hoursFromNow, bool refreshTokenNull, bool expected)
    {
        var connection = new KajabiConnection
        {
            AccessTokenExpiresAt = DateTimeOffset.UtcNow.AddHours(hoursFromNow),
            EncryptedRefreshToken = refreshTokenNull ? null : "encrypted-rt",
        };

        Assert.Equal(expected, connection.NeedsReconnect(DateTimeOffset.UtcNow));
    }
}
