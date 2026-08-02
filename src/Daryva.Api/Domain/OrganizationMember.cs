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
    /// Role within the organization. Currently only "Landlord" -- kept as a string (not an
    /// enum) so future org-scoped roles (Property Manager, Contractor) can be added without
    /// a schema change. Platform Admin is NOT a value here: it's AppUser.IsPlatformAdmin,
    /// orthogonal to org membership. Tenant is NOT a value here either: tenant portal access
    /// is a narrower, tenancy-level link, not org membership (not built yet).
    /// </summary>
    public required string Role { get; set; }

    /// <summary>
    /// True for the org's primary owner -- exclusive rights (delete org, transfer ownership,
    /// billing) that shouldn't be shared across every co-managing Landlord on the org.
    /// Exactly one member per org should have this set to true.
    /// </summary>
    public bool IsPrimaryOwner { get; set; }

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
        public const string Landlord = "Landlord";

        public static bool IsValid(string role) => role == Landlord;
    }
}
