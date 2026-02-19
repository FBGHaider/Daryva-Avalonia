namespace Daryva.Api.Domain;

/// <summary>
/// Represents a tenant organization (e.g., a landlord's business).
/// Multiple users can belong to the same organization.
/// </summary>
public class Organization
{
    /// <summary>
    /// Unique identifier for the organization.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Organization display name (e.g., "John's Property Management").
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Timestamp when organization was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Navigation: Members of this organization.
    /// </summary>
    public ICollection<OrganizationMember> Members { get; set; } = new List<OrganizationMember>();

    /// <summary>
    /// Navigation: Houses owned by this organization.
    /// </summary>
    public ICollection<House> Houses { get; set; } = new List<House>();

    /// <summary>
    /// Navigation: Pending/completed invites for this organization.
    /// </summary>
    public ICollection<OrganizationInvite> Invites { get; set; } = new List<OrganizationInvite>();

    /// <summary>
    /// Navigation: Join codes for this organization.
    /// </summary>
    public ICollection<OrganizationJoinCode> JoinCodes { get; set; } = new List<OrganizationJoinCode>();
}
