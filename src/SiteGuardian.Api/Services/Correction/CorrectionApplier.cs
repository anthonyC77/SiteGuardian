using System.Text.Json;
using SiteGuardian.Api.Models;
using SiteGuardian.Api.Services.Kajabi;

namespace SiteGuardian.Api.Services.Correction;

/// <summary>
/// Exécute un plan de correction validé (§4.2 étape 5) : appelle les tools MCP
/// d'écriture avec exactement les args du plan — jamais via le LLM — puis relit
/// pour vérifier (étape 6, sans LLM, 0 €). Revalide la whitelist en défense en
/// profondeur, indépendamment du filtrage déjà fait à la proposition du plan.
/// </summary>
public class CorrectionApplier
{
    private readonly IKajabiMcpClient _mcp;
    private readonly ILogger<CorrectionApplier> _logger;

    public CorrectionApplier(IKajabiMcpClient mcp, ILogger<CorrectionApplier> logger)
    {
        _mcp = mcp;
        _logger = logger;
    }

    public async Task ApplyAsync(CorrectionPlan plan, CancellationToken ct = default)
    {
        var applicable = plan.Actions.Where(a => !a.Blocked).ToList();

        foreach (var action in applicable)
        {
            // Défense en profondeur (§10 test 6) : jamais confiance dans un plan stocké.
            if (!CorrectionToolWhitelist.IsAllowed(action.Tool))
            {
                action.Blocked = true;
                action.BlockedReason = $"Tool « {action.Tool} » hors whitelist d'écriture (revalidé à l'exécution).";
                _logger.LogWarning("Correction : action re-bloquée à l'exécution, tool « {Tool} ».", action.Tool);
                continue;
            }

            Dictionary<string, object?> args;
            try
            {
                args = JsonSerializer.Deserialize<Dictionary<string, object?>>(action.ArgsJson) ?? new();
            }
            catch (JsonException ex)
            {
                action.ApplyError = $"Arguments invalides : {ex.Message}";
                continue;
            }

            try
            {
                var result = await _mcp.CallToolAsync(action.Tool, args, ct);
                if (result.IsError)
                {
                    action.ApplyError = result.Text;
                    continue;
                }

                action.Applied = true;
                action.AppliedAt = DateTimeOffset.UtcNow;
                await VerifyAsync(action, args, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Échec d'exécution de l'action {Tool} sur {Target}.", action.Tool, action.Target);
                action.ApplyError = ex.Message;
            }
        }

        plan.Status = DetermineStatus(applicable);
        plan.DecidedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Vérification post-écriture (§4.2 étape 6) : relit via get_* et compare au texte
    /// attendu. Uniquement disponible pour update_theme_content pour l'instant — les
    /// tools get_* équivalents pour blog/landing page/navbar ne sont pas confirmés.
    /// </summary>
    private async Task VerifyAsync(CorrectionAction action, Dictionary<string, object?> args, CancellationToken ct)
    {
        if (action.Tool != "update_theme_content" || action.After is null) return;
        if (!args.TryGetValue("site_id", out var siteId) || !args.TryGetValue("key", out var key)) return;

        try
        {
            var readBack = await _mcp.CallToolAsync(
                "get_theme_content",
                new Dictionary<string, object?> { ["site_id"] = siteId, ["key"] = key },
                ct);

            action.PostWriteVerified = !readBack.IsError && readBack.Text.Contains(action.After, StringComparison.Ordinal);
            action.VerificationNote = action.PostWriteVerified == true
                ? "Relecture MCP conforme au texte attendu."
                : $"Relecture MCP ne correspond pas au texte attendu : {Truncate(readBack.Text)}";
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            action.PostWriteVerified = null;
            action.VerificationNote = $"Vérification impossible : {ex.Message}";
        }
    }

    private static CorrectionPlanStatus DetermineStatus(List<CorrectionAction> applicable)
    {
        if (applicable.Count == 0) return CorrectionPlanStatus.Failed;
        var succeeded = applicable.Count(a => a.Applied);
        if (succeeded == applicable.Count) return CorrectionPlanStatus.Applied;
        return succeeded > 0 ? CorrectionPlanStatus.PartiallyApplied : CorrectionPlanStatus.Failed;
    }

    private static string Truncate(string s, int max = 200) => s.Length <= max ? s : s[..max] + "…";
}
