using SiteGuardian.Api.Services.Kajabi;

namespace SiteGuardian.Api.Tests.Fakes;

/// <summary>Client MCP Kajabi de test : tools et réponses programmables, sans réseau.</summary>
public class FakeKajabiMcpClient : IKajabiMcpClient
{
    public List<KajabiMcpTool> Tools { get; set; } = new();

    public Exception? ListToolsException { get; set; }

    /// <summary>Réponse par tool ; par défaut, un succès vide si aucune entrée ne correspond.</summary>
    public Func<string, IReadOnlyDictionary<string, object?>, KajabiMcpToolResult>? OnCallTool { get; set; }

    public List<(string Tool, IReadOnlyDictionary<string, object?> Args)> Calls { get; } = new();

    public Task<IReadOnlyList<KajabiMcpTool>> ListToolsAsync(CancellationToken ct = default)
    {
        if (ListToolsException is not null) throw ListToolsException;
        return Task.FromResult<IReadOnlyList<KajabiMcpTool>>(Tools);
    }

    public Task<KajabiMcpToolResult> CallToolAsync(
        string toolName, IReadOnlyDictionary<string, object?> args, CancellationToken ct = default)
    {
        Calls.Add((toolName, args));
        var result = OnCallTool?.Invoke(toolName, args) ?? new KajabiMcpToolResult("{}", false);
        return Task.FromResult(result);
    }
}
