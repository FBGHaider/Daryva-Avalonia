namespace Daryva.Api.Dtos;

/// <summary>
/// Current user profile (from AppUserProfile).
/// </summary>
public class MeUserDto
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? Phone { get; set; }
    public string? TimeZoneId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
}

/// <summary>
/// GET /api/me response: user, organisations, onboarding flags.
/// </summary>
public class MeResponseDto
{
    public MeUserDto User { get; set; } = null!;
    public List<OrganizationResponse> Organisations { get; set; } = new();
    public bool RequiresOrgSetup { get; set; }
    public bool RequiresProfileSetup { get; set; }
}
