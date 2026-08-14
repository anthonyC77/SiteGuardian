using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using SiteGuardian.Api.Data;
using SiteGuardian.Api.Hubs;
using SiteGuardian.Api.Models;
using SiteGuardian.Api.Services.Audit;
using SiteGuardian.Api.Services.Correction;
using SiteGuardian.Api.Services.Kajabi;
using SiteGuardian.Api.Services.Llm;

// --- Point d'entrée CLI (audit sans serveur web, cf. §11 du plan) -----------
// Utilisé par le cron GitHub Actions : `dotnet run -- audit <url>`.
if (args.Length > 0 && args[0].Equals("audit", StringComparison.OrdinalIgnoreCase))
{
    return await SiteGuardian.Api.Cli.AuditCommand.RunAsync(args.Skip(1).ToArray());
}

var builder = WebApplication.CreateBuilder(args);

const string FrontendCorsPolicy = "FrontendCors";

// --- Services ----------------------------------------------------------------
builder.Services.AddOpenApi();
builder.Services.AddSignalR();

builder.Services.AddDbContext<SiteGuardianDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("SiteGuardian")
                      ?? "Data Source=siteguardian.db"));

builder.Services.AddHttpClient<SitemapFetcher>(client =>
{
    client.DefaultRequestHeaders.UserAgent.ParseAdd("SiteGuardian/0.1");
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddSingleton(sp => PythonAuditService.FromConfiguration(
    sp.GetRequiredService<IConfiguration>(),
    sp.GetRequiredService<IHostEnvironment>().ContentRootPath,
    sp.GetRequiredService<ILogger<PythonAuditService>>()));
builder.Services.AddScoped<AuditRunner>();

// --- LLM (§5/§11 du plan) : Claude si clé configurée (user-secrets), sinon désactivé —
// l'audit reste alors 100 % déterministe (0 €).
builder.Services.AddSingleton<ILlmProvider>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var apiKey = config["Anthropic:ApiKey"]
                 ?? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
    return string.IsNullOrWhiteSpace(apiKey)
        ? new DisabledLlmProvider()
        : new AnthropicLlmProvider(apiKey, sp.GetRequiredService<ILogger<AnthropicLlmProvider>>());
});
builder.Services.AddSingleton(sp => SpellingPrefilter.CreateFrench(
    sp.GetRequiredService<IHostEnvironment>().ContentRootPath,
    sp.GetRequiredService<ILogger<SpellingPrefilter>>()));
builder.Services.AddSingleton(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    return new ContentAnalyzerOptions
    {
        Model = config["Agents:Surveillance:Model"] ?? "claude-sonnet-5",
        BatchPages = config.GetValue("Agents:Surveillance:BatchPages", 5),
        MaxCostEur = config.GetValue("Budget:MaxCoutEstimeParAuditEUR", 0.50m),
        InputPricePerMTokEur = config.GetValue("Budget:PrixEntreeEURParMTokens", 2.8m),
        OutputPricePerMTokEur = config.GetValue("Budget:PrixSortieEURParMTokens", 14m),
    };
});
builder.Services.AddScoped<ContentAnalyzer>();

// --- Kajabi MCP (§6/§7 du plan) : OAuth 2.1, tokens chiffrés, lecture seule en Phase 2. ---
builder.Services.AddDataProtection()
    .SetApplicationName("SiteGuardian")
    .PersistKeysToFileSystem(new DirectoryInfo(
        Path.Combine(builder.Environment.ContentRootPath, "keys")));
builder.Services.AddHttpClient<KajabiOAuthMetadataDiscovery>();
builder.Services.AddHttpClient<KajabiDynamicClientRegistrar>();
builder.Services.AddHttpClient<KajabiOAuthService>();
builder.Services.AddScoped<KajabiTokenStore>();
builder.Services.AddScoped<KajabiMcpClient>();
builder.Services.AddScoped<IKajabiMcpClient>(sp => sp.GetRequiredService<KajabiMcpClient>());

// --- Agent Correction (§4.2/§5 du plan) : boucle agentique en lecture seule,
// plan proposé validé par un humain, exécution directe (sans LLM) sur validation. ---
builder.Services.AddSingleton(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    return new CorrectionAgentOptions
    {
        Model = config["Agents:Correction:Model"] ?? "claude-haiku-4-5-20251001",
        FallbackModel = config["Agents:Correction:FallbackModel"] ?? "claude-sonnet-5",
        MaxToolCalls = config.GetValue("Agents:Correction:MaxToolCalls",
            config.GetValue("Budget:MaxToolCallsParCorrection", 15)),
        MaxTokens = config.GetValue("Budget:MaxTokensParCorrection", 50_000),
        InputPricePerMTokEur = config.GetValue("Budget:PrixEntreeEURParMTokens", 2.8m),
        OutputPricePerMTokEur = config.GetValue("Budget:PrixSortieEURParMTokens", 14m),
    };
});
builder.Services.AddScoped<CorrectionAgent>();
builder.Services.AddScoped<CorrectionApplier>();

// CORS restreint au front local + GitHub Pages (démo lecture seule), cf. §12.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                     ?? new[] { "http://localhost:4200" };
builder.Services.AddCors(options =>
    options.AddPolicy(FrontendCorsPolicy, policy =>
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials()));

var app = builder.Build();

// --- Base de données : création du schéma au démarrage (migrations en Phase 2+)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SiteGuardianDbContext>();
    db.Database.EnsureCreated();
}

// --- Pipeline ----------------------------------------------------------------
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors(FrontendCorsPolicy);

app.MapGet("/api/health", () => Results.Ok(new
{
    status = "ok",
    service = "SiteGuardian.Api",
    phase = "1 — audit"
}))
.WithName("Health");

// Lance un audit (job asynchrone, progression via SignalR /hubs/progress).
app.MapPost("/api/audit", async (
    AuditRequest request, SiteGuardianDbContext db, IServiceScopeFactory scopeFactory) =>
{
    if (!Uri.TryCreate(request.Url, UriKind.Absolute, out var uri)
        || (uri.Scheme != "http" && uri.Scheme != "https"))
    {
        return Results.BadRequest(new { error = "URL absolue http(s) requise." });
    }

    var job = new AuditJob { TargetUrl = uri.ToString() };
    db.AuditJobs.Add(job);
    await db.SaveChangesAsync();

    _ = Task.Run(async () =>
    {
        using var scope = scopeFactory.CreateScope();
        var runner = scope.ServiceProvider.GetRequiredService<AuditRunner>();
        await runner.RunAsync(job.Id);
    });

    return Results.Accepted($"/api/audit/{job.Id}", new { id = job.Id });
})
.WithName("StartAudit");

// Statut + findings d'un audit.
app.MapGet("/api/audit/{id:guid}", async (Guid id, SiteGuardianDbContext db) =>
{
    var job = await db.AuditJobs.Include(j => j.Findings)
        .FirstOrDefaultAsync(j => j.Id == id);
    return job is null ? Results.NotFound() : Results.Ok(job);
})
.WithName("GetAudit");

// Télécharge le rapport PDF d'un audit terminé.
app.MapGet("/api/audit/{id:guid}/pdf", async (Guid id, SiteGuardianDbContext db) =>
{
    var job = await db.AuditJobs.Include(j => j.Findings)
        .FirstOrDefaultAsync(j => j.Id == id);
    if (job is null) return Results.NotFound();
    if (job.Status != AuditStatus.Completed)
        return Results.Conflict(new { error = $"Audit non terminé (statut : {job.Status})." });

    var host = Uri.TryCreate(job.TargetUrl, UriKind.Absolute, out var uri) ? uri.Host : "site";
    var bytes = SiteGuardian.Api.Services.Pdf.AuditPdfGenerator.Generate(job);
    return Results.File(bytes, "application/pdf",
        $"audit-{host}-{(job.CompletedAt ?? DateTimeOffset.UtcNow):yyyy-MM-dd}.pdf");
})
.WithName("GetAuditPdf");

// Importe un rapport JSON produit par le swarm d'agents (.claude/agents).
app.MapPost("/api/audit/import", async (HttpRequest request, SiteGuardianDbContext db) =>
{
    using var reader = new StreamReader(request.Body);
    var json = await reader.ReadToEndAsync();

    List<SwarmReport> reports;
    try
    {
        reports = SwarmReportImporter.Parse(json);
    }
    catch (System.Text.Json.JsonException ex)
    {
        return Results.BadRequest(new { error = $"JSON invalide : {ex.Message}" });
    }

    if (reports.Count == 0)
        return Results.BadRequest(new { error = "Aucun rapport dans le corps de la requête." });

    var job = new AuditJob
    {
        TargetUrl = reports[0].Target,
        Source = "import",
        Status = AuditStatus.Completed,
        CompletedAt = DateTimeOffset.UtcNow,
        Findings = SwarmReportImporter.ToFindings(reports),
        PagesAudited = reports.Select(r => r.Target).Distinct().Count(),
    };
    db.AuditJobs.Add(job);
    await db.SaveChangesAsync();

    return Results.Created($"/api/audit/{job.Id}", new { id = job.Id, findings = job.Findings.Count });
})
.WithName("ImportSwarmReport");

// Démarre l'OAuth Kajabi (§7) : redirige le gestionnaire du site vers l'écran d'autorisation.
app.MapGet("/api/kajabi/connect", async (HttpRequest request, KajabiOAuthService oauth) =>
{
    var redirectUri = KajabiRedirect.CallbackUri(request);
    try
    {
        var authUrl = await oauth.BuildAuthorizationUrlAsync(redirectUri);
        // AbsoluteUri (pas ToString) : préserve l'encodage %-escape, ex. l'espace de "scope".
        return Results.Redirect(authUrl.AbsoluteUri);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Impossible de démarrer la connexion Kajabi : {ex.Message}", statusCode: 502);
    }
})
.WithName("ConnectKajabi");

// Retour de Kajabi après autorisation : échange le code contre des tokens.
app.MapGet("/api/kajabi/callback", async (
    HttpRequest request, KajabiOAuthService oauth, KajabiMcpClient mcp, KajabiTokenStore tokenStore) =>
{
    var error = request.Query["error"].ToString();
    if (!string.IsNullOrEmpty(error))
        return Results.BadRequest(new { error, description = request.Query["error_description"].ToString() });

    var code = request.Query["code"].ToString();
    var state = request.Query["state"].ToString();
    if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
        return Results.BadRequest(new { error = "Paramètres code/state manquants." });

    try
    {
        await oauth.HandleCallbackAsync(code, state, KajabiRedirect.CallbackUri(request));

        // Premier appel de toute session MCP (§6) : localise le site connecté.
        string? siteId = null;
        try
        {
            var sites = await mcp.ListSitesAsync();
            siteId = sites.FirstOrDefault()?.Id;
            if (siteId is not null)
                await tokenStore.UpdateSiteIdAsync(siteId);
        }
        catch (Exception)
        {
            // La connexion est valide même si list_sites échoue ; le site_id sera retenté plus tard.
        }

        return Results.Ok(new { connected = true, siteId });
    }
    catch (Exception ex)
    {
        return Results.Problem($"Échec de la connexion Kajabi : {ex.Message}", statusCode: 502);
    }
})
.WithName("KajabiCallback");

// Statut de connexion (§7) : connecté ? site_id ? reconnexion requise ?
app.MapGet("/api/kajabi/status", async (KajabiTokenStore tokenStore) =>
{
    var connection = await tokenStore.GetConnectionAsync();
    if (connection is null)
        return Results.Ok(new { connected = false, needsReconnect = false });

    var now = DateTimeOffset.UtcNow;
    return Results.Ok(new
    {
        connected = true,
        siteId = connection.SiteId,
        scopes = connection.Scopes,
        connectedAt = connection.ConnectedAt,
        expiresAt = connection.AccessTokenExpiresAt,
        needsReconnect = connection.NeedsReconnect(now),
    });
})
.WithName("KajabiStatus");

// Propose un plan de correction à partir d'une instruction en langage naturel (§4.2/§7).
// Toujours une préview : aucune écriture n'a lieu ici.
app.MapPost("/api/corrections", async (
    CorrectionRequest request, CorrectionAgent agent, SiteGuardianDbContext db, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(request.Instruction))
        return Results.BadRequest(new { error = "Instruction requise." });

    var plan = await agent.BuildPlanAsync(request.Instruction, ct);
    db.CorrectionPlans.Add(plan);
    await db.SaveChangesAsync(ct);

    return Results.Created($"/api/corrections/{plan.Id}", plan);
})
.WithName("ProposeCorrection");

// Historique des corrections (§7, écran Historique).
// Tri en mémoire : le provider Sqlite d'EF Core ne traduit pas ORDER BY sur
// DateTimeOffset en SQL (même limitation que KajabiOAuthService, cf. Phase 2).
app.MapGet("/api/corrections", async (SiteGuardianDbContext db) =>
{
    var plans = await db.CorrectionPlans.Include(p => p.Actions).ToListAsync();
    return Results.Ok(plans.OrderByDescending(p => p.CreatedAt));
})
.WithName("ListCorrections");

app.MapGet("/api/corrections/{id:guid}", async (Guid id, SiteGuardianDbContext db) =>
{
    var plan = await db.CorrectionPlans.Include(p => p.Actions).FirstOrDefaultAsync(p => p.Id == id);
    return plan is null ? Results.NotFound() : Results.Ok(plan);
})
.WithName("GetCorrection");

// Exécute le plan validé (§4.2 étape 5) : écriture directe backend, args exacts du plan.
app.MapPost("/api/corrections/{id:guid}/apply", async (
    Guid id, SiteGuardianDbContext db, CorrectionApplier applier, CancellationToken ct) =>
{
    var plan = await db.CorrectionPlans.Include(p => p.Actions).FirstOrDefaultAsync(p => p.Id == id, ct);
    if (plan is null) return Results.NotFound();
    if (plan.Status != CorrectionPlanStatus.AwaitingReview)
        return Results.Conflict(new { error = $"Plan non applicable (statut : {plan.Status})." });

    await applier.ApplyAsync(plan, ct);
    await db.SaveChangesAsync(ct);

    return Results.Ok(plan);
})
.WithName("ApplyCorrection");

// Journalise le refus d'un plan (§7) — aucune écriture.
app.MapPost("/api/corrections/{id:guid}/reject", async (Guid id, SiteGuardianDbContext db) =>
{
    var plan = await db.CorrectionPlans.FirstOrDefaultAsync(p => p.Id == id);
    if (plan is null) return Results.NotFound();
    if (plan.Status is not (CorrectionPlanStatus.AwaitingReview or CorrectionPlanStatus.NeedsClarification))
        return Results.Conflict(new { error = $"Plan non refusable (statut : {plan.Status})." });

    plan.Status = CorrectionPlanStatus.Rejected;
    plan.DecidedAt = DateTimeOffset.UtcNow;
    await db.SaveChangesAsync();

    return Results.Ok(plan);
})
.WithName("RejectCorrection");

app.MapHub<ProgressHub>("/hubs/progress");

app.Run();
return 0;

/// <summary>Corps de POST /api/audit.</summary>
public record AuditRequest(string Url);

/// <summary>Corps de POST /api/corrections.</summary>
public record CorrectionRequest(string Instruction);

/// <summary>Construit l'URI de callback OAuth Kajabi — doit être identique entre /connect et /callback.</summary>
public static class KajabiRedirect
{
    public static Uri CallbackUri(HttpRequest request) =>
        new($"{request.Scheme}://{request.Host}/api/kajabi/callback");
}
