namespace SiteGuardian.Api.Models;

/// <summary>
/// Plan de correction produit par l'Agent Correction (§4.2 du plan) à partir d'une
/// instruction en langage naturel. Toujours soumis à validation humaine avant
/// exécution — l'écriture ne part jamais directement du LLM.
/// </summary>
public class CorrectionPlan
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Instruction en langage naturel saisie par l'utilisateur.</summary>
    public string Instruction { get; set; } = string.Empty;

    /// <summary>Résumé lisible du plan (ex. « 1 remplacement dans le bandeau, visible sur 43 pages »).</summary>
    public string? Resume { get; set; }

    public CorrectionPlanStatus Status { get; set; } = CorrectionPlanStatus.AwaitingReview;

    /// <summary>Question posée par l'agent quand l'instruction est ambiguë (§10, test 5) — aucune écriture.</summary>
    public string? Clarification { get; set; }

    public string ModelUsed { get; set; } = string.Empty;

    /// <summary>Escalade vers le modèle de repli si Haiku échoue (§5).</summary>
    public bool EscalatedToFallback { get; set; }

    /// <summary>Compteurs pour le garde-fou budgétaire (§11).</summary>
    public long InputTokens { get; set; }

    public long OutputTokens { get; set; }

    public int ToolCallCount { get; set; }

    public decimal EstimatedCostEur { get; set; }

    /// <summary>Renseigné si l'agent n'a pas pu produire de plan (budget dépassé, erreur MCP…).</summary>
    public string? Error { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? DecidedAt { get; set; }

    public List<CorrectionAction> Actions { get; set; } = new();
}

public enum CorrectionPlanStatus
{
    /// <summary>Plan proposé, en attente de validation/refus humain.</summary>
    AwaitingReview,

    /// <summary>Instruction ambiguë : l'agent a posé une question au lieu de produire un plan.</summary>
    NeedsClarification,

    Applied,

    /// <summary>Certaines actions ont échoué à l'exécution malgré la validation (erreur MCP).</summary>
    PartiallyApplied,

    Rejected,

    /// <summary>L'agent n'a pas pu produire de plan exploitable (budget, erreur).</summary>
    Failed,
}
