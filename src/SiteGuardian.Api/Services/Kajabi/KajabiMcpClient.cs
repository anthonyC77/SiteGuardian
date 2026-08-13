using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace SiteGuardian.Api.Services.Kajabi;

/// <summary>
/// Client MCP vers le serveur Kajabi (§6 du plan) : lecture seule pour la Phase 2
/// (list_sites, get_theme_content). L'écriture (update_theme_content, etc.) arrive
/// en Phase 3, derrière la validation humaine du plan de correction.
/// </summary>
public class KajabiMcpClient
{
    private readonly KajabiOAuthService _oauth;
    private readonly IConfiguration _config;
    private readonly ILoggerFactory _loggerFactory;

    public KajabiMcpClient(KajabiOAuthService oauth, IConfiguration config, ILoggerFactory loggerFactory)
    {
        _oauth = oauth;
        _config = config;
        _loggerFactory = loggerFactory;
    }

    private Uri McpServerUrl => new(_config["Kajabi:McpUrl"] ?? "https://mcp.kajabi.com/mcp");

    /// <summary>Ouvre une session MCP authentifiée. Premier appel de toute session : list_sites (§6).</summary>
    private async Task<McpClient> ConnectAsync(CancellationToken ct)
    {
        var accessToken = await _oauth.GetValidAccessTokenAsync(ct);

        var transport = new HttpClientTransport(new HttpClientTransportOptions
        {
            Endpoint = McpServerUrl,
            Name = "SiteGuardian",
            AdditionalHeaders = new Dictionary<string, string> { ["Authorization"] = $"Bearer {accessToken}" },
        });

        return await McpClient.CreateAsync(transport, loggerFactory: _loggerFactory, cancellationToken: ct);
    }

    public async Task<IReadOnlyList<KajabiSite>> ListSitesAsync(CancellationToken ct = default)
    {
        await using var client = await ConnectAsync(ct);
        var result = await client.CallToolAsync("list_sites", new Dictionary<string, object?>(), cancellationToken: ct);
        var json = ExtractText(result);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement.ValueKind == JsonValueKind.Array
            ? doc.RootElement
            : doc.RootElement.TryGetProperty("sites", out var sites) ? sites : doc.RootElement;

        var list = new List<KajabiSite>();
        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var element in root.EnumerateArray())
            {
                var id = element.TryGetProperty("id", out var idProp) ? idProp.GetRawText().Trim('"') : "";
                var name = element.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? "" : "";
                if (!string.IsNullOrEmpty(id))
                    list.Add(new KajabiSite(id, name));
            }
        }
        return list;
    }

    /// <summary>Récupère le contenu d'un bloc de thème (bandeau, footer…) — lecture seule.</summary>
    public async Task<string> GetThemeContentAsync(string siteId, string contentKey, CancellationToken ct = default)
    {
        await using var client = await ConnectAsync(ct);
        var result = await client.CallToolAsync(
            "get_theme_content",
            new Dictionary<string, object?> { ["site_id"] = siteId, ["key"] = contentKey },
            cancellationToken: ct);
        return ExtractText(result);
    }

    private static string ExtractText(CallToolResult result)
    {
        if (result.IsError == true)
            throw new InvalidOperationException(
                $"Le serveur MCP Kajabi a renvoyé une erreur : {ExtractRawText(result)}");
        return ExtractRawText(result);
    }

    private static string ExtractRawText(CallToolResult result) =>
        string.Concat(result.Content.OfType<TextContentBlock>().Select(b => b.Text));
}

public record KajabiSite(string Id, string Name);
