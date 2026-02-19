namespace Daryva.Api.Security;

/// <summary>
/// Configuration for JWT Bearer token validation.
/// </summary>
public class JwtOptions
{
    public string? Authority { get; set; }
    public string Audience { get; set; } = "daryva-api";
    public string Issuer { get; set; } = "daryva-api";
    public string SigningKey { get; set; } = "CHANGE_ME_SUPER_SECRET_MIN_32_CHARS";
    public int AccessTokenMinutes { get; set; } = 30;
    public int RefreshTokenDays { get; set; } = 14;
}
