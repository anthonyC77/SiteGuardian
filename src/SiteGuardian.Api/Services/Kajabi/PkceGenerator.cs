using System.Security.Cryptography;
using System.Text;

namespace SiteGuardian.Api.Services.Kajabi;

/// <summary>PKCE (RFC 7636), obligatoire en OAuth 2.1 — génère verifier/challenge et le paramètre state.</summary>
public static class PkceGenerator
{
    /// <summary>Verifier aléatoire de 64 octets encodés en base64url (RFC 7636 : 43–128 caractères).</summary>
    public static string CreateCodeVerifier() => Base64UrlEncode(RandomNumberGenerator.GetBytes(64));

    /// <summary>Challenge S256 = base64url(SHA-256(verifier)).</summary>
    public static string CreateCodeChallenge(string codeVerifier) =>
        Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier)));

    /// <summary>Paramètre « state » anti-CSRF, imprévisible.</summary>
    public static string CreateState() => Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
