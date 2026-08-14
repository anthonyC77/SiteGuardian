using System.Text.Json;
using System.Text.Json.Serialization;
using SiteGuardian.Api.Models;
using SiteGuardian.Api.Services.Kajabi;
using SiteGuardian.Api.Services.Llm;

namespace SiteGuardian.Api.Services.Correction;

public record CorrectionAgentOptions
{
    public string Model { get; init; } = "claude-haiku-4-5-20251001";
    public string FallbackModel { get; init; } = "claude-sonnet-5";

    /// <summary>
    /// Seuil de tool calls avant escalade Haiku → Sonnet (§5), et budget à nouveau
    /// accordé au modèle de repli après escalade. Le plan documente une seule
    /// valeur (15) à la fois sous Agents:Correction et sous Budget — traitée ici
    /// comme un seul et même réglage plutôt que deux seuils distincts.
    /// </summary>
    public int MaxToolCalls { get; init; } = 15;

    public int MaxTokens { get; init; } = 50_000;

    public decimal InputPricePerMTokEur { get; init; } = 2.8m;
    public decimal OutputPricePerMTokEur { get; init; } = 14m;
}

/// <summary>
/// Agent Correction (§4.2 du plan) : transforme une instruction en langage naturel
/// en <see cref="CorrectionPlan"/> via une boucle agentique en lecture seule.
/// L'écriture ne part jamais du LLM — seulement du plan validé par un humain,
/// exécuté ensuite tel quel par le backend (cf. endpoints /apply).
/// </summary>
public class CorrectionAgent
{
    private const string ProposePlanTool = "propose_correction_plan";
    private const string RequestClarificationTool = "request_clarification";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly ILlmProvider _llm;
    private readonly IKajabiMcpClient _mcp;
    private readonly CorrectionAgentOptions _options;
    private readonly ILogger<CorrectionAgent> _logger;

    public CorrectionAgent(
        ILlmProvider llm, IKajabiMcpClient mcp, CorrectionAgentOptions options, ILogger<CorrectionAgent> logger)
    {
        _llm = llm;
        _mcp = mcp;
        _options = options;
        _logger = logger;
    }

    public async Task<CorrectionPlan> BuildPlanAsync(string instruction, CancellationToken ct = default)
    {
        var plan = new CorrectionPlan { Instruction = instruction, ModelUsed = _options.Model };

        if (!_llm.IsEnabled)
        {
            plan.Status = CorrectionPlanStatus.Failed;
            plan.Error = "Aucune clé API Anthropic configurée.";
            return plan;
        }

        IReadOnlyList<KajabiMcpTool> allTools;
        try
        {
            allTools = await _mcp.ListToolsAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Impossible de lister les tools MCP Kajabi.");
            plan.Status = CorrectionPlanStatus.Failed;
            plan.Error = $"Connexion à Kajabi impossible : {ex.Message}";
            return plan;
        }

        var readOnlyTools = allTools.Where(t => IsReadOnlyToolName(t.Name)).ToList();
        var tools = BuildToolDefinitions(readOnlyTools);

        var messages = new List<LlmMessage> { LlmMessage.User(new LlmTextBlock(instruction)) };
        var model = _options.Model;
        var escalated = false;
        var toolCallsThisAttempt = 0;

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            if (plan.InputTokens + plan.OutputTokens > _options.MaxTokens)
            {
                plan.Status = CorrectionPlanStatus.Failed;
                plan.Error = $"Budget de tokens dépassé ({_options.MaxTokens} tokens) — analyse incomplète.";
                break;
            }

            if (toolCallsThisAttempt >= _options.MaxToolCalls)
            {
                if (!escalated)
                {
                    _logger.LogWarning(
                        "Correction : {Model} n'a pas conclu après {N} tool calls — escalade vers {Fallback}.",
                        model, toolCallsThisAttempt, _options.FallbackModel);
                    model = _options.FallbackModel;
                    escalated = true;
                    plan.EscalatedToFallback = true;
                    plan.ModelUsed = model;
                    toolCallsThisAttempt = 0;
                }
                else
                {
                    plan.Status = CorrectionPlanStatus.Failed;
                    plan.Error = "L'agent n'a pas produit de plan exploitable après escalade — trop d'appels d'outils.";
                    break;
                }
            }

            LlmToolUseResponse response;
            try
            {
                // Instantané de l'historique : `messages` continue d'être muté après cet appel
                // (tool_use puis tool_result y sont ajoutés) — un provider qui garde la référence
                // de la requête ne doit pas voir son historique changer rétroactivement.
                response = await _llm.CompleteWithToolsAsync(
                    new LlmToolUseRequest(model, SystemPrompt, messages.ToList(), tools), ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Échec de l'appel LLM pendant la localisation.");
                plan.Status = CorrectionPlanStatus.Failed;
                plan.Error = $"Échec de l'appel au modèle : {ex.Message}";
                break;
            }

            plan.InputTokens += response.Usage.InputTokens;
            plan.OutputTokens += response.Usage.OutputTokens;
            plan.EstimatedCostEur =
                (plan.InputTokens * _options.InputPricePerMTokEur
                 + plan.OutputTokens * _options.OutputPricePerMTokEur) / 1_000_000m;

            messages.Add(new LlmMessage("assistant", response.Content));

            var proposePlanCall = response.ToolUses.FirstOrDefault(t => t.Name == ProposePlanTool);
            if (proposePlanCall is not null)
            {
                ApplyProposedPlan(plan, proposePlanCall.Input);
                break;
            }

            var clarifyCall = response.ToolUses.FirstOrDefault(t => t.Name == RequestClarificationTool);
            if (clarifyCall is not null)
            {
                var question = clarifyCall.Input.TryGetProperty("question", out var q)
                    ? q.GetString() : null;
                plan.Status = CorrectionPlanStatus.NeedsClarification;
                plan.Clarification = string.IsNullOrWhiteSpace(question)
                    ? "L'agent a besoin de précisions sur votre demande."
                    : question;
                break;
            }

            var toolUses = response.ToolUses.ToList();
            if (toolUses.Count == 0)
            {
                // Le modèle a répondu sans appeler de tool : ni plan ni clarification.
                plan.Status = CorrectionPlanStatus.Failed;
                plan.Error = "Le modèle n'a proposé ni plan ni clarification.";
                break;
            }

            var results = new List<LlmContentBlock>();
            foreach (var toolUse in toolUses)
            {
                toolCallsThisAttempt++;
                plan.ToolCallCount++;
                results.Add(await ExecuteReadOnlyToolAsync(toolUse, readOnlyTools, ct));
            }
            messages.Add(new LlmMessage("user", results));
        }

        return plan;
    }

    private async Task<LlmToolResultBlock> ExecuteReadOnlyToolAsync(
        LlmToolUseBlock toolUse, IReadOnlyList<KajabiMcpTool> readOnlyTools, CancellationToken ct)
    {
        if (readOnlyTools.All(t => t.Name != toolUse.Name))
            return new LlmToolResultBlock(toolUse.Id, "Tool non autorisé en phase de lecture.", IsError: true);

        try
        {
            var args = JsonSerializer.Deserialize<Dictionary<string, object?>>(toolUse.Input.GetRawText())
                ?? new Dictionary<string, object?>();
            var result = await _mcp.CallToolAsync(toolUse.Name, args, ct);
            return new LlmToolResultBlock(toolUse.Id, result.Text, result.IsError);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Échec de l'appel MCP {Tool}", toolUse.Name);
            return new LlmToolResultBlock(toolUse.Id, $"Erreur : {ex.Message}", IsError: true);
        }
    }

    private void ApplyProposedPlan(CorrectionPlan plan, JsonElement input)
    {
        ProposePlanArgs? args;
        try
        {
            args = JsonSerializer.Deserialize<ProposePlanArgs>(input.GetRawText(), JsonOptions);
        }
        catch (JsonException ex)
        {
            plan.Status = CorrectionPlanStatus.Failed;
            plan.Error = $"Plan renvoyé par le modèle illisible : {ex.Message}";
            return;
        }

        if (args is null || args.Actions.Count == 0)
        {
            plan.Status = CorrectionPlanStatus.Failed;
            plan.Error = "Le modèle a proposé un plan sans action.";
            return;
        }

        plan.Resume = args.Resume;
        foreach (var a in args.Actions)
        {
            var allowed = CorrectionToolWhitelist.IsAllowed(a.Tool);
            var action = new CorrectionAction
            {
                Tool = a.Tool,
                Target = a.Cible,
                Before = a.Avant,
                After = a.Apres,
                ArgsJson = a.Args.ValueKind == JsonValueKind.Undefined ? "{}" : a.Args.GetRawText(),
                Blocked = !allowed,
                BlockedReason = allowed ? null : $"Tool « {a.Tool} » hors whitelist d'écriture.",
            };
            plan.Actions.Add(action);

            if (!allowed)
                _logger.LogWarning(
                    "Correction : action bloquée, tool hors whitelist « {Tool} » (instruction : {Instruction}).",
                    a.Tool, plan.Instruction);
        }

        plan.Status = plan.Actions.All(a => a.Blocked)
            ? CorrectionPlanStatus.Failed
            : CorrectionPlanStatus.AwaitingReview;
        if (plan.Status == CorrectionPlanStatus.Failed)
            plan.Error = "Toutes les actions proposées utilisent des tools hors whitelist d'écriture.";
    }

    private static bool IsReadOnlyToolName(string name) =>
        name.StartsWith("list_", StringComparison.Ordinal)
        || name.StartsWith("get_", StringComparison.Ordinal)
        || name.StartsWith("search_", StringComparison.Ordinal);

    private static List<LlmToolDefinition> BuildToolDefinitions(IReadOnlyList<KajabiMcpTool> readOnlyTools)
    {
        var tools = readOnlyTools
            .Select(t => new LlmToolDefinition(t.Name, t.Description ?? t.Name, t.InputSchema))
            .ToList();

        tools.Add(new LlmToolDefinition(ProposePlanTool,
            "Soumet le plan de correction final pour validation humaine. À appeler une fois la localisation " +
            "terminée, avec un ou plusieurs remplacements précis.", ProposePlanSchema));

        tools.Add(new LlmToolDefinition(RequestClarificationTool,
            "Pose une question à l'utilisateur quand l'instruction est trop ambiguë pour produire un plan " +
            "fiable (ex. « améliore le SEO » sans précision). N'invente jamais de plan dans ce cas.",
            RequestClarificationSchema));

        return tools;
    }

    private const string SystemPrompt = """
        Tu es l'Agent Correction d'un site Kajabi. Un gestionnaire de site te donne une
        instruction en langage naturel pour corriger un texte publié (faute, lien cassé,
        mention obsolète…). Ton rôle se limite à la LOCALISATION : retrouver le texte exact
        à corriger en utilisant les tools de lecture disponibles (list_*, get_*, search_*).

        Règles strictes :
        - Tu n'as accès à AUCUN tool d'écriture. Tu ne modifies jamais rien toi-même.
        - Une fois le texte localisé avec certitude, appelle `propose_correction_plan` avec
          un plan précis : pour chaque remplacement, le tool MCP d'écriture à utiliser
          (ex. update_theme_content, update_blog_post, update_landing_page, update_navbar_link),
          la cible (où), le texte avant, le texte après, et les arguments exacts pour le tool.
        - Si l'instruction est ambiguë ou ne permet pas de localiser un texte précis (ex.
          « améliore le SEO » sans détail), n'invente rien : appelle `request_clarification`
          avec une question précise plutôt que de proposer un plan approximatif.
        - Un même texte peut apparaître sur plusieurs pages via un seul élément de thème
          (bandeau, footer) : une seule action suffit alors, mentionne le nombre de pages
          concernées dans le résumé.
        """;

    private static readonly JsonElement ProposePlanSchema = JsonDocument.Parse("""
        {
          "type": "object",
          "properties": {
            "resume": { "type": "string", "description": "Résumé lisible du plan pour l'utilisateur" },
            "actions": {
              "type": "array",
              "items": {
                "type": "object",
                "properties": {
                  "tool": { "type": "string", "description": "Nom du tool MCP d'écriture" },
                  "cible": { "type": "string", "description": "Description de la cible (ex. « Announcement Bar — site theme »)" },
                  "avant": { "type": "string", "description": "Texte actuel" },
                  "apres": { "type": "string", "description": "Texte corrigé proposé" },
                  "args": { "type": "object", "description": "Arguments exacts à passer au tool MCP" }
                },
                "required": ["tool", "cible", "avant", "apres", "args"]
              }
            }
          },
          "required": ["resume", "actions"]
        }
        """).RootElement.Clone();

    private static readonly JsonElement RequestClarificationSchema = JsonDocument.Parse("""
        {
          "type": "object",
          "properties": {
            "question": { "type": "string", "description": "Question précise à poser à l'utilisateur" }
          },
          "required": ["question"]
        }
        """).RootElement.Clone();

    private class ProposePlanArgs
    {
        public string? Resume { get; set; }
        public List<ProposedAction> Actions { get; set; } = new();
    }

    private class ProposedAction
    {
        public string Tool { get; set; } = string.Empty;
        public string Cible { get; set; } = string.Empty;
        public string? Avant { get; set; }
        public string? Apres { get; set; }
        public JsonElement Args { get; set; }
    }
}
