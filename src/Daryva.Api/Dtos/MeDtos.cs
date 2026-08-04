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

    /// <summary>AppUser.IsPlatformAdmin -- false for AppUserProfile-only (OIDC/Dev) identities, which have no AppUser row.</summary>
    public bool IsPlatformAdmin { get; set; }
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

/// <summary>
/// PUT /api/me request: allowed profile fields only (DisplayName, Phone, TimeZoneId).
/// </summary>
public class UpdateMeRequest
{
    /// <summary>Full name or display name. Max 256 characters.</summary>
    public string? DisplayName { get; set; }

    /// <summary>Phone number (optional). Basic validation: digits, +, spaces, max 50.</summary>
    public string? Phone { get; set; }

    /// <summary>IANA or Windows time zone id (e.g. "GMT Standard Time"). Max 128 characters.</summary>
    public string? TimeZoneId { get; set; }
}
