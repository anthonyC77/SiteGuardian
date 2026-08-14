using SiteGuardian.Api.Services.Correction;

namespace SiteGuardian.Api.Tests;

public class CorrectionToolWhitelistTests
{
    [Theory]
    [InlineData("update_theme_content")]
    [InlineData("update_blog_post")]
    [InlineData("update_landing_page")]
    [InlineData("update_navbar_link")]
    public void KnownWriteTools_AreAllowed(string tool)
    {
        Assert.True(CorrectionToolWhitelist.IsAllowed(tool));
    }

    [Theory]
    [InlineData("delete_site")]
    [InlineData("update_theme_content ")] // espace parasite
    [InlineData("UPDATE_THEME_CONTENT")] // casse différente
    [InlineData("")]
    public void UnknownOrMalformedTools_AreRejected(string tool)
    {
        Assert.False(CorrectionToolWhitelist.IsAllowed(tool));
    }
}
