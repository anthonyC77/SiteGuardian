using Microsoft.EntityFrameworkCore;
using SiteGuardian.Api.Models;

namespace SiteGuardian.Api.Data;

public class SiteGuardianDbContext : DbContext
{
    public SiteGuardianDbContext(DbContextOptions<SiteGuardianDbContext> options)
        : base(options)
    {
    }

    public DbSet<AuditJob> AuditJobs => Set<AuditJob>();

    public DbSet<Finding> Findings => Set<Finding>();

    public DbSet<CorrectionPlan> CorrectionPlans => Set<CorrectionPlan>();

    public DbSet<CorrectionAction> CorrectionActions => Set<CorrectionAction>();

    public DbSet<PageTextHash> PageTextHashes => Set<PageTextHash>();

    public DbSet<KajabiConnection> KajabiConnections => Set<KajabiConnection>();

    public DbSet<KajabiOAuthState> KajabiOAuthStates => Set<KajabiOAuthState>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuditJob>()
            .HasMany(a => a.Findings)
            .WithOne(f => f.AuditJob)
            .HasForeignKey(f => f.AuditJobId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<KajabiOAuthState>().HasKey(s => s.State);

        modelBuilder.Entity<CorrectionPlan>()
            .HasMany(p => p.Actions)
            .WithOne(a => a.CorrectionPlan)
            .HasForeignKey(a => a.CorrectionPlanId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
