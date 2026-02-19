namespace Daryva.Api.Domain;

/// <summary>
/// Represents a user's membership and role within an organization.
/// Supports RBAC: Owner, Admin, Member, ReadOnly.
/// </summary>
public class OrganizationMember
{
    /// <summary>
    /// Unique identifier for the membership record.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// FK: Organization this member belongs to.
    /// </summary>
    public Guid OrganizationId { get; set; }

    /// <summary>
    /// User ID (from auth provider or internal).
    /// </summary>
    public required string UserId { get; set; }

    /// <summary>
    /// Email address (optional; stored for invite reference or notification).
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// Role within the organization: "Owner", "Admin", "Member", or "ReadOnly".
    /// Keep as string to support external role systems; can be validated at application layer.
    /// </summary>
    public required string Role { get; set; }

    /// <summary>
    /// Timestamp when member joined the organization.
    /// </summary>
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Navigation: Organization reference.
    /// </summary>
    public Organization? Organization { get; set; }

    // Common role constants for convenience
    public static class Roles
    {
        public const string Owner = "Owner";
        public const string Admin = "Admin";
        public const string Member = "Member";
        public const string ReadOnly = "ReadOnly";

        public static bool IsValid(string role) =>
            role == Owner || role == Admin || role == Member || role == ReadOnly;
    }
}
