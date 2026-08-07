using System.Security.Cryptography;
using System.Text;

namespace Daryva.Api.Security;

/// <summary>
/// Shared token generation/hashing for every "email a link, redeem a one-time token" flow
/// (email verification, password reset, tenant invites) - one implementation of a
/// security-sensitive primitive rather than a copy per flow. Callers store only Hash(token);
/// the plaintext token goes in the email/link and is never persisted.
/// </summary>
public static class TokenGenerator
{
    public static string GenerateToken()
    {
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        return raw.TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    public static string Hash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes);
    }
}
