using System.Net;
using SiteGuardian.Api.Services.Kajabi;
using SiteGuardian.Api.Tests.Fakes;

namespace SiteGuardian.Api.Tests;

public class KajabiOAuthMetadataDiscoveryTests
{
    private static readonly Uri McpUrl = new("https://mcp.kajabi.com/mcp");

    [Fact]
    public async Task ProtectedResourceMetadata_PointsToAuthServer_UsesItsEndpoints()
    {
        var handler = FakeHttpMessageHandler.MapJson(new()
        {
            ["https://mcp.kajabi.com/.well-known/oauth-protected-resource/mcp"] = (HttpStatusCode.OK, """
                { "authorization_servers": ["https://auth.kajabi.com"] }
                """),
            ["https://auth.kajabi.com/.well-known/oauth-authorization-server"] = (HttpStatusCode.OK, """
                {
                  "authorization_endpoint": "https://auth.kajabi.com/authorize",
                  "token_endpoint": "https://auth.kajabi.com/token",
                  "registration_endpoint": "https://auth.kajabi.com/register"
                }
                """),
        });
        var discovery = new KajabiOAuthMetadataDiscovery(handler.ToHttpClient());

        var metadata = await discovery.DiscoverAsync(McpUrl);

        Assert.Equal("https://auth.kajabi.com/authorize", metadata.AuthorizationEndpoint);
        Assert.Equal("https://auth.kajabi.com/token", metadata.TokenEndpoint);
        Assert.Equal("https://auth.kajabi.com/register", metadata.RegistrationEndpoint);
    }

    [Fact]
    public async Task NoProtectedResourceMetadata_FallsBackToSameOrigin()
    {
        // Pas de /.well-known/oauth-protected-resource : le serveur MCP est lui-même le serveur d'autorisation.
        var handler = FakeHttpMessageHandler.MapJson(new()
        {
            ["https://mcp.kajabi.com/.well-known/oauth-authorization-server"] = (HttpStatusCode.OK, """
                {
                  "authorization_endpoint": "https://mcp.kajabi.com/authorize",
                  "token_endpoint": "https://mcp.kajabi.com/token"
                }
                """),
        });
        var discovery = new KajabiOAuthMetadataDiscovery(handler.ToHttpClient());

        var metadata = await discovery.DiscoverAsync(McpUrl);

        Assert.Equal("https://mcp.kajabi.com/authorize", metadata.AuthorizationEndpoint);
        Assert.Null(metadata.RegistrationEndpoint);
    }

    [Fact]
    public async Task MissingAuthorizationServerMetadata_Throws()
    {
        var handler = FakeHttpMessageHandler.MapJson(new());
        var discovery = new KajabiOAuthMetadataDiscovery(handler.ToHttpClient());

        await Assert.ThrowsAnyAsync<Exception>(() => discovery.DiscoverAsync(McpUrl));
    }

    [Fact]
    public async Task MetadataMissingEndpoints_ThrowsInvalidOperation()
    {
        var handler = FakeHttpMessageHandler.MapJson(new()
        {
            ["https://mcp.kajabi.com/.well-known/oauth-authorization-server"] = (HttpStatusCode.OK, "{}"),
        });
        var discovery = new KajabiOAuthMetadataDiscovery(handler.ToHttpClient());

        await Assert.ThrowsAsync<InvalidOperationException>(() => discovery.DiscoverAsync(McpUrl));
    }
}
