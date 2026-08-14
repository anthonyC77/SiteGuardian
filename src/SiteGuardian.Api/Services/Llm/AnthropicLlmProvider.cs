using System.Text;
using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;

namespace SiteGuardian.Api.Services.Llm;

/// <summary>
/// Implémentation Claude via le SDK C# officiel (package NuGet « Anthropic »).
/// </summary>
public sealed class AnthropicLlmProvider : ILlmProvider
{
    private readonly AnthropicClient _client;
    private readonly ILogger<AnthropicLlmProvider> _logger;

    public AnthropicLlmProvider(string apiKey, ILogger<AnthropicLlmProvider> logger)
    {
        _client = new AnthropicClient { ApiKey = apiKey };
        _logger = logger;
    }

    public bool IsEnabled => true;

    public async Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken ct = default)
    {
        var message = await _client.Messages.Create(
            new MessageCreateParams
            {
                Model = request.Model,
                MaxTokens = request.MaxTokens,
                System = request.SystemPrompt,
                Messages =
                [
                    new MessageParam { Role = Role.User, Content = request.UserMessage },
                ],
            },
            cancellationToken: ct);

        var sb = new StringBuilder();
        foreach (var block in message.Content)
        {
            if (block.TryPickText(out var textBlock))
                sb.Append(textBlock.Text);
        }

        var usage = new LlmUsage(message.Usage.InputTokens, message.Usage.OutputTokens);
        _logger.LogInformation(
            "Appel LLM {Model} : {In} tokens entrée, {Out} tokens sortie",
            request.Model, usage.InputTokens, usage.OutputTokens);

        return new LlmResponse(sb.ToString(), usage);
    }

    public async Task<LlmToolUseResponse> CompleteWithToolsAsync(
        LlmToolUseRequest request, CancellationToken ct = default)
    {
        var tools = request.Tools
            .Select(t => new Tool
            {
                Name = t.Name,
                Description = t.Description,
                InputSchema = ToInputSchema(t.InputSchema),
            })
            .ToList();

        var messages = request.Messages.Select(ToMessageParam).ToList();

        var message = await _client.Messages.Create(
            new MessageCreateParams
            {
                Model = request.Model,
                MaxTokens = request.MaxTokens,
                System = request.SystemPrompt,
                Messages = messages,
                Tools = tools.Select(t => (ToolUnion)t).ToList(),
            },
            cancellationToken: ct);

        var content = new List<LlmContentBlock>();
        foreach (var block in message.Content)
        {
            if (block.TryPickText(out var textBlock))
                content.Add(new LlmTextBlock(textBlock.Text));
            else if (block.TryPickToolUse(out var toolUseBlock))
                content.Add(new LlmToolUseBlock(
                    toolUseBlock.ID, toolUseBlock.Name, ToJsonElement(toolUseBlock.Input)));
        }

        var usage = new LlmUsage(message.Usage.InputTokens, message.Usage.OutputTokens);
        _logger.LogInformation(
            "Appel LLM tool-use {Model} : {In} tokens entrée, {Out} tokens sortie, stop={Stop}",
            request.Model, usage.InputTokens, usage.OutputTokens, message.StopReason);

        return new LlmToolUseResponse(message.StopReason?.ToString() ?? "end_turn", content, usage);
    }

    private static MessageParam ToMessageParam(LlmMessage message)
    {
        var blocks = message.Content.Select(ToContentBlockParam).ToList();
        return new MessageParam
        {
            Role = message.Role == "assistant" ? Role.Assistant : Role.User,
            Content = blocks,
        };
    }

    private static ContentBlockParam ToContentBlockParam(LlmContentBlock block) => block switch
    {
        LlmTextBlock text => new TextBlockParam { Text = text.Text },
        LlmToolUseBlock toolUse => new ToolUseBlockParam
        {
            ID = toolUse.Id,
            Name = toolUse.Name,
            Input = ToArgsDict(toolUse.Input),
        },
        LlmToolResultBlock toolResult => new ToolResultBlockParam
        {
            ToolUseID = toolResult.ToolUseId,
            Content = toolResult.Content,
            IsError = toolResult.IsError,
        },
        _ => throw new NotSupportedException($"Bloc de contenu non supporté : {block.GetType().Name}"),
    };

    /// <summary>Schéma JSON d'une définition de tool (Tool.InputSchema).</summary>
    private static InputSchema ToInputSchema(JsonElement element) => InputSchema.FromRawUnchecked(ToArgsDict(element));

    /// <summary>Arguments d'un appel de tool (ToolUseBlockParam.Input / ToolUseBlock.Input).</summary>
    private static IReadOnlyDictionary<string, JsonElement> ToArgsDict(JsonElement element) =>
        JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(element.GetRawText())
        ?? new Dictionary<string, JsonElement>();

    private static JsonElement ToJsonElement(IReadOnlyDictionary<string, JsonElement> dict) =>
        JsonDocument.Parse(JsonSerializer.Serialize(dict)).RootElement;
}
