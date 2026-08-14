using System.Text.Json;

namespace SiteGuardian.Api.Services.Llm;

/// <summary>
/// Abstraction LLM (cf. §5 du plan) : permet de brancher plus tard un provider
/// local (Ollama) derrière la même interface que Claude.
/// </summary>
public interface ILlmProvider
{
    /// <summary>False si aucune clé API n'est configurée — l'analyse LLM est alors sautée.</summary>
    bool IsEnabled { get; }

    Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken ct = default);

    /// <summary>
    /// Un tour de boucle agentique (tool-use, §4.2) : le modèle reçoit l'historique
    /// de conversation + les tools disponibles, et répond soit avec du texte, soit
    /// en demandant l'exécution d'un ou plusieurs tools.
    /// </summary>
    Task<LlmToolUseResponse> CompleteWithToolsAsync(LlmToolUseRequest request, CancellationToken ct = default);
}

public record LlmRequest(
    string Model,
    string SystemPrompt,
    string UserMessage,
    int MaxTokens = 4096);

public record LlmResponse(string Text, LlmUsage Usage);

public record LlmUsage(long InputTokens, long OutputTokens);

public record LlmToolDefinition(string Name, string Description, JsonElement InputSchema);

public record LlmToolUseRequest(
    string Model,
    string SystemPrompt,
    IReadOnlyList<LlmMessage> Messages,
    IReadOnlyList<LlmToolDefinition> Tools,
    int MaxTokens = 4096);

/// <summary>Un tour de conversation : "user" ou "assistant", avec un ou plusieurs blocs de contenu.</summary>
public record LlmMessage(string Role, IReadOnlyList<LlmContentBlock> Content)
{
    public static LlmMessage User(params LlmContentBlock[] blocks) => new("user", blocks);
    public static LlmMessage Assistant(params LlmContentBlock[] blocks) => new("assistant", blocks);
}

public abstract record LlmContentBlock;

public record LlmTextBlock(string Text) : LlmContentBlock;

/// <summary>Le modèle demande l'exécution d'un tool.</summary>
public record LlmToolUseBlock(string Id, string Name, JsonElement Input) : LlmContentBlock;

/// <summary>Résultat d'un tool renvoyé au modèle (toujours dans un message "user").</summary>
public record LlmToolResultBlock(string ToolUseId, string Content, bool IsError = false) : LlmContentBlock;

public record LlmToolUseResponse(
    string StopReason, IReadOnlyList<LlmContentBlock> Content, LlmUsage Usage)
{
    public IEnumerable<LlmToolUseBlock> ToolUses => Content.OfType<LlmToolUseBlock>();

    public string TextOnly => string.Concat(Content.OfType<LlmTextBlock>().Select(b => b.Text));
}

/// <summary>Provider inactif quand aucune clé n'est configurée (l'audit reste 100 % déterministe).</summary>
public sealed class DisabledLlmProvider : ILlmProvider
{
    public bool IsEnabled => false;

    public Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken ct = default) =>
        throw new InvalidOperationException(
            "Aucune clé API Anthropic configurée (Anthropic:ApiKey ou ANTHROPIC_API_KEY).");

    public Task<LlmToolUseResponse> CompleteWithToolsAsync(LlmToolUseRequest request, CancellationToken ct = default) =>
        throw new InvalidOperationException(
            "Aucune clé API Anthropic configurée (Anthropic:ApiKey ou ANTHROPIC_API_KEY).");
}
