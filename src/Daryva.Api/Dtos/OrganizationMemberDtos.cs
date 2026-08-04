namespace Daryva.Api.Dtos;

/// <summary>
/// Request payload for adding a member to an organization.
/// </summary>
public class AddMemberRequest
{
    /// <summary>
    /// Email address of the user to invite.
    /// Will be matched against OrganizationMember.Email for now.
    /// Future: Could integrate with Auth0 user directory.
    /// </summary>
    public required string Email { get; set; }

    /// <summary>
    /// Role for the new member. Only "Landlord" is currently valid -- see
    /// OrganizationMember.Roles.IsValid.
    /// </summary>
    public required string Role { get; set; }
}

/// <summary>
/// Response payload for an organization member.
/// </summary>
public class OrganizationMemberResponse
{
    /// <summary>
    /// Unique identifier for this membership record.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// User's ID (from auth provider).
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// User's email address (from member record or AppUserProfile).
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// User's display name (from AppUserProfile when available).
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Role within the organization.
    /// </summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// True for the org's primary owner -- exclusive rights (rename org, delete org, transfer
    /// ownership) that aren't shared across every co-managing Landlord. Exactly one member per
    /// org should have this set.
    /// </summary>
    public bool IsPrimaryOwner { get; set; }

    /// <summary>
    /// Timestamp when member joined the organization.
    /// </summary>
    public DateTime JoinedAt { get; set; }
}

/// <summary>
/// Organization row for a platform admin's Support Mode org browse -- not membership-scoped,
/// unlike OrganizationResponse.
/// </summary>
public class AdminOrganizationSummaryResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string? OwnerEmail { get; set; }
    public int MemberCount { get; set; }
}

/// <summary>One matched member (by email) and the org(s) they belong to -- Support Mode finds the
/// landlord first, then the admin picks which of their orgs to act on.</summary>
public class AdminMemberSearchResultResponse
{
    public string Email { get; set; } = string.Empty;
    public List<AdminOrganizationSummaryResponse> Organizations { get; set; } = new();
}

public class AdminOrgEmailSearchResponse
{
    public List<AdminMemberSearchResultResponse> Matches { get; set; } = new();
}
