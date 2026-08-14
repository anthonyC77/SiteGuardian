using Microsoft.Extensions.Logging.Abstractions;
using SiteGuardian.Api.Models;
using SiteGuardian.Api.Services.Correction;
using SiteGuardian.Api.Services.Kajabi;
using SiteGuardian.Api.Tests.Fakes;

namespace SiteGuardian.Api.Tests;

public class CorrectionApplierTests
{
    private static CorrectionApplier CreateApplier(FakeKajabiMcpClient mcp) =>
        new(mcp, NullLogger<CorrectionApplier>.Instance);

    private static CorrectionAction ThemeContentAction(string after = "accompagnement") => new()
    {
        Tool = "update_theme_content",
        Target = "Announcement Bar",
        Before = "acccompagnement",
        After = after,
        ArgsJson = """{"site_id":"s1","key":"announcement_bar"}""",
    };

    [Fact]
    public async Task SuccessfulApply_VerifiesPostWrite_MarksPlanApplied()
    {
        var mcp = new FakeKajabiMcpClient
        {
            OnCallTool = (tool, _) => tool switch
            {
                "update_theme_content" => new KajabiMcpToolResult("ok", false),
                "get_theme_content" => new KajabiMcpToolResult("Bienvenue, notre accompagnement est unique.", false),
                _ => new KajabiMcpToolResult("{}", false),
            },
        };
        var plan = new CorrectionPlan { Status = CorrectionPlanStatus.AwaitingReview };
        plan.Actions.Add(ThemeContentAction());

        await CreateApplier(mcp).ApplyAsync(plan);

        Assert.Equal(CorrectionPlanStatus.Applied, plan.Status);
        Assert.True(plan.Actions[0].Applied);
        Assert.True(plan.Actions[0].PostWriteVerified);
        Assert.NotNull(plan.Actions[0].AppliedAt);
        Assert.NotNull(plan.DecidedAt);
    }

    [Fact]
    public async Task VerificationMismatch_AppliedButNotVerified()
    {
        var mcp = new FakeKajabiMcpClient
        {
            OnCallTool = (tool, _) => tool switch
            {
                "update_theme_content" => new KajabiMcpToolResult("ok", false),
                "get_theme_content" => new KajabiMcpToolResult("Toujours la faute : acccompagnement.", false),
                _ => new KajabiMcpToolResult("{}", false),
            },
        };
        var plan = new CorrectionPlan { Status = CorrectionPlanStatus.AwaitingReview };
        plan.Actions.Add(ThemeContentAction());

        await CreateApplier(mcp).ApplyAsync(plan);

        Assert.True(plan.Actions[0].Applied); // l'écriture a réussi côté MCP
        Assert.False(plan.Actions[0].PostWriteVerified); // mais la relecture ne correspond pas
        Assert.NotNull(plan.Actions[0].VerificationNote);
    }

    [Fact]
    public async Task McpWriteFails_ActionNotApplied_PlanFailed()
    {
        var mcp = new FakeKajabiMcpClient
        {
            OnCallTool = (_, _) => new KajabiMcpToolResult("Erreur serveur Kajabi", true),
        };
        var plan = new CorrectionPlan { Status = CorrectionPlanStatus.AwaitingReview };
        plan.Actions.Add(ThemeContentAction());

        await CreateApplier(mcp).ApplyAsync(plan);

        Assert.Equal(CorrectionPlanStatus.Failed, plan.Status);
        Assert.False(plan.Actions[0].Applied);
        Assert.Equal("Erreur serveur Kajabi", plan.Actions[0].ApplyError);
    }

    [Fact]
    public async Task MixedOutcomes_PartiallyApplied()
    {
        var callCount = 0;
        var mcp = new FakeKajabiMcpClient
        {
            OnCallTool = (tool, _) =>
            {
                if (tool != "update_theme_content") return new KajabiMcpToolResult("{}", false);
                callCount++;
                return callCount == 1
                    ? new KajabiMcpToolResult("ok", false)
                    : new KajabiMcpToolResult("échec", true);
            },
        };
        var plan = new CorrectionPlan { Status = CorrectionPlanStatus.AwaitingReview };
        plan.Actions.Add(ThemeContentAction());
        plan.Actions.Add(new CorrectionAction
        {
            Tool = "update_theme_content", Target = "Footer", ArgsJson = """{"site_id":"s1","key":"footer"}""",
        });

        await CreateApplier(mcp).ApplyAsync(plan);

        Assert.Equal(CorrectionPlanStatus.PartiallyApplied, plan.Status);
        Assert.True(plan.Actions[0].Applied);
        Assert.False(plan.Actions[1].Applied);
    }

    [Fact]
    public async Task AlreadyBlockedAction_IsSkipped_NoMcpCall()
    {
        var mcp = new FakeKajabiMcpClient();
        var plan = new CorrectionPlan { Status = CorrectionPlanStatus.AwaitingReview };
        plan.Actions.Add(new CorrectionAction { Tool = "delete_site", Blocked = true, BlockedReason = "hors whitelist" });

        await CreateApplier(mcp).ApplyAsync(plan);

        Assert.Empty(mcp.Calls);
        Assert.False(plan.Actions[0].Applied);
        Assert.Equal(CorrectionPlanStatus.Failed, plan.Status); // aucune action applicable
    }

    [Fact]
    public async Task DefenseInDepth_RevalidatesWhitelistAtApplyTime()
    {
        // Une action non bloquée au moment du plan (bug hypothétique en amont) mais dont le
        // tool n'est pas dans la whitelist doit quand même être bloquée ici, jamais exécutée.
        var mcp = new FakeKajabiMcpClient();
        var plan = new CorrectionPlan { Status = CorrectionPlanStatus.AwaitingReview };
        plan.Actions.Add(new CorrectionAction { Tool = "delete_site", Blocked = false, ArgsJson = "{}" });

        await CreateApplier(mcp).ApplyAsync(plan);

        Assert.Empty(mcp.Calls);
        Assert.True(plan.Actions[0].Blocked);
        Assert.False(plan.Actions[0].Applied);
    }

    [Fact]
    public async Task NonThemeContentTool_SkipsAutomaticVerification()
    {
        var mcp = new FakeKajabiMcpClient
        {
            OnCallTool = (_, _) => new KajabiMcpToolResult("ok", false),
        };
        var plan = new CorrectionPlan { Status = CorrectionPlanStatus.AwaitingReview };
        plan.Actions.Add(new CorrectionAction
        {
            Tool = "update_blog_post", Target = "Article", After = "finitude", ArgsJson = "{}",
        });

        await CreateApplier(mcp).ApplyAsync(plan);

        Assert.True(plan.Actions[0].Applied);
        Assert.Null(plan.Actions[0].PostWriteVerified);
        // Un seul appel MCP : l'écriture. Pas de relecture pour ce tool.
        Assert.Single(mcp.Calls);
    }
}
