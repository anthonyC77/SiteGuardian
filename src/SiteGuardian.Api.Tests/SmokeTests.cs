using Microsoft.EntityFrameworkCore;
using SiteGuardian.Api.Data;
using SiteGuardian.Api.Models;

namespace SiteGuardian.Api.Tests;

/// <summary>
/// Tests de fumée : le DbContext persiste les entités avec le schéma swarm.
/// </summary>
public class SmokeTests
{
    private static SiteGuardianDbContext NewInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<SiteGuardianDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        var context = new SiteGuardianDbContext(options);
        context.Database.OpenConnection();
        context.Database.EnsureCreated();
        return context;
    }

    [Fact]
    public void AuditJob_WithFindings_IsPersisted()
    {
        using var context = NewInMemoryContext();

        var job = new AuditJob { TargetUrl = "https://example.com" };
        job.Findings.Add(new Finding
        {
            FindingId = "content-typo",
            Title = "Faute d'orthographe dans le bandeau",
            Category = "content",
            Severity = FindingSeverity.High,
            Fix = "Corriger le texte du bandeau",
            SourceAgent = "web-audit-script",
            Pages = new List<string> { "https://example.com/", "https://example.com/blog" },
            PageCount = 2,
        });

        context.AuditJobs.Add(job);
        context.SaveChanges();

        var reloaded = context.AuditJobs.Include(a => a.Findings).Single();
        Assert.Single(reloaded.Findings);
        Assert.Equal(2, reloaded.Findings[0].PageCount);
        Assert.Equal(2, reloaded.Findings[0].Pages.Count);
        Assert.Equal(FindingSeverity.High, reloaded.Findings[0].Severity);
    }

    [Fact]
    public void CorrectionPlan_DefaultStatus_IsAwaitingReview()
    {
        var plan = new CorrectionPlan { Instruction = "corrige accompagnement partout" };
        Assert.Equal(CorrectionPlanStatus.AwaitingReview, plan.Status);
    }

    [Fact]
    public void CorrectionPlan_WithActions_IsPersisted()
    {
        using var context = NewInMemoryContext();

        var plan = new CorrectionPlan
        {
            Instruction = "corrige acccompagnement partout",
            Resume = "1 remplacement dans le bandeau, visible sur 43 pages",
        };
        plan.Actions.Add(new CorrectionAction
        {
            Tool = "update_theme_content",
            Target = "Announcement Bar — site theme",
            Before = "…acccompagnement…",
            After = "…accompagnement…",
            ArgsJson = "{\"site_id\":\"s1\",\"key\":\"announcement_bar\"}",
        });

        context.CorrectionPlans.Add(plan);
        context.SaveChanges();

        var reloaded = context.CorrectionPlans.Include(p => p.Actions).Single();
        Assert.Single(reloaded.Actions);
        Assert.Equal("update_theme_content", reloaded.Actions[0].Tool);
        Assert.False(reloaded.Actions[0].Applied);
    }
}
