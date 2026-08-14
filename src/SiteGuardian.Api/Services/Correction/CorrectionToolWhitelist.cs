namespace SiteGuardian.Api.Services.Correction;

/// <summary>
/// Whitelist des tools MCP d'écriture autorisés (§4.2 du plan). Vérifiée à deux
/// endroits indépendants — quand l'agent propose un plan, et à nouveau juste avant
/// l'exécution (§10 test 6) — jamais de confiance aveugle dans un plan stocké.
/// </summary>
public static class CorrectionToolWhitelist
{
    public static readonly IReadOnlySet<string> WriteTools = new HashSet<string>(StringComparer.Ordinal)
    {
        "update_theme_content",
        "update_blog_post",
        "update_landing_page",
        "update_navbar_link",
    };

    public static bool IsAllowed(string toolName) => WriteTools.Contains(toolName);
}
