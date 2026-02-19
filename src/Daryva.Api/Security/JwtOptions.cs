namespace Daryva.Api.Security;

/// <summary>
/// Configuration for JWT Bearer token validation.
/// </summary>
public class JwtOptions
{
    public string? Authority { get; set; }
    public string? Audience { get; set; }
}
