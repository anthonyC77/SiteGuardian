using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace SiteGuardian.Api.Services.Kajabi;

/// <summary>
/// Client MCP vers le serveur Kajabi (§6/§4.2 du plan). Chaque appel ouvre une
/// session courte (list_sites/get_theme_content en Phase 2 ; générique en Phase 3
/// pour l'Agent Correction) — plus simple à raisonner qu'une session partagée sur
/// toute la durée d'une boucle agentique, au prix d'une reconnexion par appel.
/// </summary>
public class KajabiMcpClient : IKajabiMcpClient
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

    public async Task<IReadOnlyList<KajabiMcpTool>> ListToolsAsync(CancellationToken ct = default)
    {
        await using var client = await ConnectAsync(ct);
        var tools = await client.ListToolsAsync(cancellationToken: ct);
        return tools.Select(t => new KajabiMcpTool(t.Name, t.Description, t.JsonSchema)).ToList();
    }

    public async Task<KajabiMcpToolResult> CallToolAsync(
        string toolName, IReadOnlyDictionary<string, object?> args, CancellationToken ct = default)
    {
        await using var client = await ConnectAsync(ct);
        var result = await client.CallToolAsync(toolName, args, cancellationToken: ct);
        return new KajabiMcpToolResult(ExtractRawText(result), result.IsError == true);
    }

    public async Task<IReadOnlyList<KajabiSite>> ListSitesAsync(CancellationToken ct = default)
    {
        var result = await CallToolAsync("list_sites", new Dictionary<string, object?>(), ct);
        if (result.IsError)
            throw new InvalidOperationException($"Le serveur MCP Kajabi a renvoyé une erreur : {result.Text}");

        using var doc = JsonDocument.Parse(result.Text);
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
        var result = await CallToolAsync(
            "get_theme_content",
            new Dictionary<string, object?> { ["site_id"] = siteId, ["key"] = contentKey },
            ct);
        if (result.IsError)
            throw new InvalidOperationException($"Le serveur MCP Kajabi a renvoyé une erreur : {result.Text}");
        return result.Text;
    }

    private static string ExtractRawText(CallToolResult result) =>
        string.Concat(result.Content.OfType<TextContentBlock>().Select(b => b.Text));
}

public record KajabiSite(string Id, string Name);
