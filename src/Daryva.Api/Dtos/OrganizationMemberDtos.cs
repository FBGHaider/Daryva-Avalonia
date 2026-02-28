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
    /// Role for the new member: "Owner", "Admin", "Member", "ReadOnly".
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
    /// Timestamp when member joined the organization.
    /// </summary>
    public DateTime JoinedAt { get; set; }
}
