namespace SiteGuardian.Api.Models;

/// <summary>
/// Une action individuelle d'un <see cref="CorrectionPlan"/> : un appel à un tool
/// MCP d'écriture, avec le avant/après pour l'affichage du diff (§4.2) et le
/// journal (qui, quoi, avant/après, quand → rollback manuel possible).
/// </summary>
public class CorrectionAction
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CorrectionPlanId { get; set; }

    public CorrectionPlan? CorrectionPlan { get; set; }

    /// <summary>Nom du tool MCP (ex. « update_theme_content ») — doit être dans la whitelist d'écriture.</summary>
    public string Tool { get; set; } = string.Empty;

    /// <summary>Description humaine de la cible (ex. « Announcement Bar — site theme »).</summary>
    public string Target { get; set; } = string.Empty;

    public string? Before { get; set; }

    public string? After { get; set; }

    /// <summary>Arguments exacts à passer au tool MCP à l'exécution (JSON), validés tels quels par l'humain.</summary>
    public string ArgsJson { get; set; } = "{}";

    /// <summary>Filtré côté backend car hors whitelist (§4.2/§10 test 6) — jamais exécuté.</summary>
    public bool Blocked { get; set; }

    public string? BlockedReason { get; set; }

    public bool Applied { get; set; }

    public DateTimeOffset? AppliedAt { get; set; }

    /// <summary>Vérification post-écriture (§4.2 étape 6) : relecture via get_* comparée à « After ».</summary>
    public bool? PostWriteVerified { get; set; }

    public string? VerificationNote { get; set; }

    /// <summary>Erreur survenue à l'exécution malgré la validation (ex. panne réseau MCP).</summary>
    public string? ApplyError { get; set; }
}
