using SiteGuardian.Api.Services.Llm;

namespace SiteGuardian.Api.Tests.Fakes;

/// <summary>Provider de test pour le tool-use (Phase 3) : réponses préprogrammées, une par appel.</summary>
public class FakeToolUseLlmProvider : ILlmProvider
{
    private readonly Queue<LlmToolUseResponse> _responses;

    public FakeToolUseLlmProvider(params LlmToolUseResponse[] responses) => _responses = new(responses);

    public bool IsEnabled { get; set; } = true;

    public List<LlmToolUseRequest> ReceivedRequests { get; } = new();

    public Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken ct = default) =>
        throw new NotSupportedException("FakeToolUseLlmProvider ne gère pas CompleteAsync — voir FakeLlmProvider.");

    public Task<LlmToolUseResponse> CompleteWithToolsAsync(LlmToolUseRequest request, CancellationToken ct = default)
    {
        ReceivedRequests.Add(request);
        if (_responses.Count == 0)
            throw new InvalidOperationException("FakeToolUseLlmProvider : aucune réponse programmée restante.");
        return Task.FromResult(_responses.Dequeue());
    }
}
