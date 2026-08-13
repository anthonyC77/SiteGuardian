using SiteGuardian.Api.Services.Kajabi;

namespace SiteGuardian.Api.Tests;

public class PkceGeneratorTests
{
    [Fact]
    public void CreateCodeVerifier_IsUrlSafe_NoPadding()
    {
        var verifier = PkceGenerator.CreateCodeVerifier();
        Assert.DoesNotContain('+', verifier);
        Assert.DoesNotContain('/', verifier);
        Assert.DoesNotContain('=', verifier);
        Assert.InRange(verifier.Length, 43, 128);
    }

    [Fact]
    public void CreateCodeVerifier_IsRandomEachTime()
    {
        Assert.NotEqual(PkceGenerator.CreateCodeVerifier(), PkceGenerator.CreateCodeVerifier());
    }

    [Fact]
    public void CreateCodeChallenge_MatchesRfc7636TestVector()
    {
        // Vecteur de test officiel RFC 7636 Appendix B.
        const string verifier = "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk";
        const string expectedChallenge = "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM";

        Assert.Equal(expectedChallenge, PkceGenerator.CreateCodeChallenge(verifier));
    }

    [Fact]
    public void CreateState_IsUrlSafe_AndRandom()
    {
        var state1 = PkceGenerator.CreateState();
        var state2 = PkceGenerator.CreateState();

        Assert.NotEqual(state1, state2);
        Assert.DoesNotContain('+', state1);
        Assert.DoesNotContain('/', state1);
        Assert.DoesNotContain('=', state1);
    }
}
