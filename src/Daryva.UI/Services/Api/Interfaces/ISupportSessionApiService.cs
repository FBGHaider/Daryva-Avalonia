using System.Text.Json.Serialization;

namespace Daryva.Services.Api;

/// <summary>Organization row for a platform admin's Support Mode org browse. Not membership-scoped.</summary>
public class AdminOrganizationSummaryDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("ownerEmail")]
    public string? OwnerEmail { get; set; }

    [JsonPropertyName("memberCount")]
    public int MemberCount { get; set; }
}

/// <summary>One matched member (by email) and the org(s) they belong to -- find the landlord
/// first, then pick which of their orgs to act on.</summary>
public class AdminMemberSearchResultDto
{
    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("organizations")]
    public List<AdminOrganizationSummaryDto> Organizations { get; set; } = new();
}

public class AdminOrgEmailSearchResultDto
{
    [JsonPropertyName("matches")]
    public List<AdminMemberSearchResultDto> Matches { get; set; } = new();
}

public class SupportAccessCodeDto
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("expiresAt")]
    public DateTime ExpiresAt { get; set; }
}

public class ResolvedSupportAccessCodeDto
{
    [JsonPropertyName("organizationId")]
    public Guid OrganizationId { get; set; }

    [JsonPropertyName("organizationName")]
    public string OrganizationName { get; set; } = string.Empty;
}

public class SupportSessionDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("adminUserId")]
    public Guid AdminUserId { get; set; }

    [JsonPropertyName("organizationId")]
    public Guid OrganizationId { get; set; }

    [JsonPropertyName("organizationName")]
    public string OrganizationName { get; set; } = string.Empty;

    [JsonPropertyName("reason")]
    public string Reason { get; set; } = string.Empty;

    [JsonPropertyName("startedAt")]
    public DateTime StartedAt { get; set; }

    [JsonPropertyName("expiresAt")]
    public DateTime ExpiresAt { get; set; }

    [JsonPropertyName("endedAt")]
    public DateTime? EndedAt { get; set; }

    [JsonPropertyName("endedReason")]
    public string? EndedReason { get; set; }

    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; }
}

/// <summary>
/// Platform-admin Support Mode: browse orgs (not membership-scoped) and start/end/list time-boxed,
/// audited Support Sessions. Every method here requires the signed-in account to hold
/// AppUser.IsPlatformAdmin server-side -- the API returns 403 for anyone else.
/// </summary>
public interface ISupportSessionApiService
{
    Task<AdminOrgEmailSearchResultDto> SearchOrganizationsByEmailAsync(string search, CancellationToken cancellationToken = default);
    Task<SupportSessionDto> StartSessionAsync(Guid organizationId, string reason, int? durationMinutes = null, CancellationToken cancellationToken = default);
    Task<SupportSessionDto?> EndSessionAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task<List<SupportSessionDto>> ListSessionsAsync(Guid? organizationId = null, bool includeEnded = false, CancellationToken cancellationToken = default);

    /// <summary>Landlord side: generate a short-lived code for the caller's current org.</summary>
    Task<SupportAccessCodeDto> GenerateAccessCodeAsync(CancellationToken cancellationToken = default);

    /// <summary>Admin side: resolve a code to the org it identifies. Null if invalid/expired/already used.</summary>
    Task<ResolvedSupportAccessCodeDto?> ResolveAccessCodeAsync(string code, CancellationToken cancellationToken = default);
}
