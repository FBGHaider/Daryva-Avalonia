using System.Text.Json.Serialization;

namespace Daryva.Services.Api;

/// <summary>
/// GET /api/me response (SaaS). Matches Daryva.Api MeResponseDto.
/// </summary>
public class MeResponseDto
{
    [JsonPropertyName("user")]
    public MeUserDto User { get; set; } = null!;

    [JsonPropertyName("organisations")]
    public List<MeOrganisationDto> Organisations { get; set; } = new();

    [JsonPropertyName("requiresOrgSetup")]
    public bool RequiresOrgSetup { get; set; }

    [JsonPropertyName("requiresProfileSetup")]
    public bool RequiresProfileSetup { get; set; }
}

public class MeUserDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("phone")]
    public string? Phone { get; set; }

    [JsonPropertyName("timeZoneId")]
    public string? TimeZoneId { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("lastLoginAt")]
    public DateTime? LastLoginAt { get; set; }
}

public class MeOrganisationDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("currentUserRole")]
    public string? CurrentUserRole { get; set; }
}
