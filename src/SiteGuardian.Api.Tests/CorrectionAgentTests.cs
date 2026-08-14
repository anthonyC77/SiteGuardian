using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using SiteGuardian.Api.Models;
using SiteGuardian.Api.Services.Correction;
using SiteGuardian.Api.Services.Kajabi;
using SiteGuardian.Api.Services.Llm;
using SiteGuardian.Api.Tests.Fakes;

namespace SiteGuardian.Api.Tests;

public class CorrectionAgentTests
{
    private static readonly KajabiMcpTool GetThemeContentTool = new(
        "get_theme_content", "Lit un bloc de contenu du thème",
        JsonSerializer.SerializeToElement(new { type = "object", properties = new { } }));

    private static CorrectionAgentOptions Options(int maxToolCalls = 15, int maxTokens = 50_000) => new()
    {
        Model = "claude-haiku-4-5-20251001",
        FallbackModel = "claude-sonnet-5",
        MaxToolCalls = maxToolCalls,
        MaxTokens = maxTokens,
    };

    private static CorrectionAgent CreateAgent(
        FakeToolUseLlmProvider llm, FakeKajabiMcpClient mcp, CorrectionAgentOptions? options = null) =>
        new(llm, mcp, options ?? Options(), NullLogger<CorrectionAgent>.Instance);

    private static JsonElement ToElement(object value) => JsonSerializer.SerializeToElement(value);

    private static LlmToolUseResponse ToolUseTurn(string toolName, object input, string id = "call-1") =>
        new("tool_use", [new LlmToolUseBlock(id, toolName, ToElement(input))], new LlmUsage(500, 100));

    private static LlmToolUseResponse ProposePlanTurn(object planArgs) =>
        ToolUseTurn("propose_correction_plan", planArgs, "propose-1");

    private static LlmToolUseResponse ClarifyTurn(string question) =>
        ToolUseTurn("request_clarification", new { question }, "clarify-1");

    private static LlmToolUseResponse TextOnlyTurn(string text) =>
        new("end_turn", [new LlmTextBlock(text)], new LlmUsage(100, 50));

    [Fact]
    public async Task HappyPath_LocatesTypoAndProposesSingleAction()
    {
        var mcp = new FakeKajabiMcpClient
        {
            Tools = [GetThemeContentTool],
            OnCallTool = (_, _) => new KajabiMcpToolResult("« …acccompagnement… »", false),
        };
        var llm = new FakeToolUseLlmProvider(
            ToolUseTurn("get_theme_content", new { site_id = "s1", key = "announcement_bar" }),
            ProposePlanTurn(new
            {
                resume = "1 remplacement dans le bandeau, visible sur 43 pages",
                actions = new[]
                {
                    new
                    {
                        tool = "update_theme_content",
                        cible = "Announcement Bar — site theme",
                        avant = "…acccompagnement…",
                        apres = "…accompagnement…",
                        args = new { site_id = "s1", key = "announcement_bar" },
                    },
                },
            }));

        var plan = await CreateAgent(llm, mcp).BuildPlanAsync("corrige acccompagnement partout");

        Assert.Equal(CorrectionPlanStatus.AwaitingReview, plan.Status);
        Assert.Equal("1 remplacement dans le bandeau, visible sur 43 pages", plan.Resume);
        var action = Assert.Single(plan.Actions);
        Assert.Equal("update_theme_content", action.Tool);
        Assert.False(action.Blocked);
        Assert.Equal("…accompagnement…", action.After);
        Assert.Single(mcp.Calls); // le seul appel MCP est le get_theme_content de localisation
    }

    [Fact]
    public async Task DisallowedTool_IsBlockedAndPlanFails()
    {
        var mcp = new FakeKajabiMcpClient { Tools = [GetThemeContentTool] };
        var llm = new FakeToolUseLlmProvider(
            ProposePlanTurn(new
            {
                resume = "Suppression du site",
                actions = new[]
                {
                    new { tool = "delete_site", cible = "Site entier", avant = "", apres = "", args = new { } },
                },
            }));

        var plan = await CreateAgent(llm, mcp).BuildPlanAsync("supprime le site");

        Assert.Equal(CorrectionPlanStatus.Failed, plan.Status);
        Assert.Contains("whitelist", plan.Error);
        var action = Assert.Single(plan.Actions);
        Assert.True(action.Blocked);
        Assert.Contains("delete_site", action.BlockedReason);
    }

    [Fact]
    public async Task MixedActions_OnlyDisallowedOneIsBlocked_PlanStillAwaitsReview()
    {
        var mcp = new FakeKajabiMcpClient { Tools = [] };
        var llm = new FakeToolUseLlmProvider(
            ProposePlanTurn(new
            {
                resume = "Deux actions",
                actions = new[]
                {
                    new { tool = "update_theme_content", cible = "Footer", avant = "a", apres = "b", args = new { } },
                    new { tool = "delete_site", cible = "Site", avant = "", apres = "", args = new { } },
                },
            }));

        var plan = await CreateAgent(llm, mcp).BuildPlanAsync("corrige le footer et supprime le site");

        Assert.Equal(CorrectionPlanStatus.AwaitingReview, plan.Status);
        Assert.Equal(2, plan.Actions.Count);
        Assert.False(plan.Actions[0].Blocked);
        Assert.True(plan.Actions[1].Blocked);
    }

    [Fact]
    public async Task AmbiguousInstruction_AsksForClarification_NoActions()
    {
        var mcp = new FakeKajabiMcpClient { Tools = [] };
        var llm = new FakeToolUseLlmProvider(
            ClarifyTurn("Quelle partie du SEO souhaitez-vous améliorer (titres, meta descriptions, alt) ?"));

        var plan = await CreateAgent(llm, mcp).BuildPlanAsync("améliore le SEO");

        Assert.Equal(CorrectionPlanStatus.NeedsClarification, plan.Status);
        Assert.Contains("SEO", plan.Clarification);
        Assert.Empty(plan.Actions);
    }

    [Fact]
    public async Task TextOnlyReply_NoToolCall_MarksPlanFailed()
    {
        var mcp = new FakeKajabiMcpClient { Tools = [] };
        var llm = new FakeToolUseLlmProvider(TextOnlyTurn("Je ne sais pas quoi faire."));

        var plan = await CreateAgent(llm, mcp).BuildPlanAsync("fais quelque chose");

        Assert.Equal(CorrectionPlanStatus.Failed, plan.Status);
        Assert.NotNull(plan.Error);
    }

    [Fact]
    public async Task ToolNotInReadOnlyList_ResultIsErrorFedBack_LoopContinues()
    {
        var mcp = new FakeKajabiMcpClient { Tools = [GetThemeContentTool] }; // "search_blog" absent volontairement
        var llm = new FakeToolUseLlmProvider(
            ToolUseTurn("search_blog", new { query = "finitiude" }),
            ClarifyTurn("Je n'ai pas pu localiser l'article."));

        var plan = await CreateAgent(llm, mcp).BuildPlanAsync("corrige la catégorie finitiude");

        Assert.Equal(CorrectionPlanStatus.NeedsClarification, plan.Status);
        Assert.Empty(mcp.Calls); // le tool non autorisé n'a jamais été appelé côté MCP
        var secondRequest = llm.ReceivedRequests[1];
        var toolResultMessage = secondRequest.Messages.Last();
        var resultBlock = Assert.IsType<LlmToolResultBlock>(Assert.Single(toolResultMessage.Content));
        Assert.True(resultBlock.IsError);
        Assert.Contains("non autorisé", resultBlock.Content);
    }

    [Fact]
    public async Task TokenBudgetExceeded_StopsWithFailedStatus()
    {
        var mcp = new FakeKajabiMcpClient { Tools = [GetThemeContentTool] };
        var llm = new FakeToolUseLlmProvider(
            ToolUseTurn("get_theme_content", new { site_id = "s1" }), // usage 500+100 dans le helper
            ToolUseTurn("get_theme_content", new { site_id = "s1" }),
            ToolUseTurn("get_theme_content", new { site_id = "s1" }));

        var plan = await CreateAgent(llm, mcp, Options(maxTokens: 700)).BuildPlanAsync("corrige X");

        Assert.Equal(CorrectionPlanStatus.Failed, plan.Status);
        Assert.Contains("Budget", plan.Error);
        Assert.True(llm.ReceivedRequests.Count <= 2, "Doit s'arrêter dès que le budget est dépassé.");
    }

    [Fact]
    public async Task ExceedsMaxToolCalls_EscalatesToFallbackModel_ThenSucceeds()
    {
        var mcp = new FakeKajabiMcpClient
        {
            Tools = [GetThemeContentTool],
            OnCallTool = (_, _) => new KajabiMcpToolResult("contenu", false),
        };
        var llm = new FakeToolUseLlmProvider(
            ToolUseTurn("get_theme_content", new { site_id = "s1" }), // épuise le budget Haiku (MaxToolCalls=1)
            ProposePlanTurn(new
            {
                resume = "Corrigé après escalade",
                actions = new[]
                {
                    new { tool = "update_theme_content", cible = "Footer", avant = "a", apres = "b", args = new { } },
                },
            }));

        var plan = await CreateAgent(llm, mcp, Options(maxToolCalls: 1)).BuildPlanAsync("corrige X");

        Assert.Equal(CorrectionPlanStatus.AwaitingReview, plan.Status);
        Assert.True(plan.EscalatedToFallback);
        Assert.Equal("claude-sonnet-5", plan.ModelUsed);
        Assert.Equal("claude-sonnet-5", llm.ReceivedRequests.Last().Model);
    }

    [Fact]
    public async Task ExceedsMaxToolCallsTwice_FailsAfterEscalation()
    {
        var mcp = new FakeKajabiMcpClient
        {
            Tools = [GetThemeContentTool],
            OnCallTool = (_, _) => new KajabiMcpToolResult("contenu", false),
        };
        var llm = new FakeToolUseLlmProvider(
            ToolUseTurn("get_theme_content", new { site_id = "s1" }), // Haiku épuisé
            ToolUseTurn("get_theme_content", new { site_id = "s1" })); // Sonnet épuisé aussi

        var plan = await CreateAgent(llm, mcp, Options(maxToolCalls: 1)).BuildPlanAsync("corrige X");

        Assert.Equal(CorrectionPlanStatus.Failed, plan.Status);
        Assert.Contains("escalade", plan.Error);
    }

    [Fact]
    public async Task LlmDisabled_ReturnsFailedImmediately_NoMcpCalls()
    {
        var mcp = new FakeKajabiMcpClient { Tools = [GetThemeContentTool] };
        var llm = new FakeToolUseLlmProvider { IsEnabled = false };

        var plan = await CreateAgent(llm, mcp).BuildPlanAsync("corrige X");

        Assert.Equal(CorrectionPlanStatus.Failed, plan.Status);
        Assert.Empty(mcp.Calls);
        Assert.Empty(llm.ReceivedRequests);
    }

    [Fact]
    public async Task KajabiUnreachable_ReturnsFailedWithConnectionError()
    {
        var mcp = new FakeKajabiMcpClient { ListToolsException = new HttpRequestException("DNS failure") };
        var llm = new FakeToolUseLlmProvider();

        var plan = await CreateAgent(llm, mcp).BuildPlanAsync("corrige X");

        Assert.Equal(CorrectionPlanStatus.Failed, plan.Status);
        Assert.Contains("Kajabi", plan.Error);
        Assert.Empty(llm.ReceivedRequests);
    }

    [Fact]
    public async Task OnlyReadOnlyToolsAreExposedToTheModel()
    {
        var mcp = new FakeKajabiMcpClient
        {
            Tools =
            [
                GetThemeContentTool,
                new KajabiMcpTool("update_theme_content", "Écrit un bloc de thème", ToElement(new { })),
                new KajabiMcpTool("list_blog_posts", "Liste les articles", ToElement(new { })),
            ],
        };
        var llm = new FakeToolUseLlmProvider(ClarifyTurn("besoin de précisions"));

        await CreateAgent(llm, mcp).BuildPlanAsync("corrige X");

        var exposedNames = llm.ReceivedRequests[0].Tools.Select(t => t.Name).ToList();
        Assert.Contains("get_theme_content", exposedNames);
        Assert.Contains("list_blog_posts", exposedNames);
        Assert.DoesNotContain("update_theme_content", exposedNames);
        Assert.Contains("propose_correction_plan", exposedNames);
        Assert.Contains("request_clarification", exposedNames);
    }
}
