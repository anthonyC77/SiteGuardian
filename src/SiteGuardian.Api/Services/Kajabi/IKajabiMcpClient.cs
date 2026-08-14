using System.Text.Json;

namespace SiteGuardian.Api.Services.Kajabi;

/// <summary>
/// Accès générique au serveur MCP Kajabi : liste des tools disponibles et appel
/// d'un tool par nom. Utilisé par l'Agent Correction (§4.2) pour la phase de
/// localisation (lecture) et, derrière la whitelist, pour l'exécution validée
/// (écriture). Interface séparée de <see cref="KajabiMcpClient"/> pour permettre
/// aux tests de substituer un client MCP factice sans réseau.
/// </summary>
public interface IKajabiMcpClient
{
    Task<IReadOnlyList<KajabiMcpTool>> ListToolsAsync(CancellationToken ct = default);

    Task<KajabiMcpToolResult> CallToolAsync(
        string toolName, IReadOnlyDictionary<string, object?> args, CancellationToken ct = default);
}

public record KajabiMcpTool(string Name, string? Description, JsonElement InputSchema);

public record KajabiMcpToolResult(string Text, bool IsError);
