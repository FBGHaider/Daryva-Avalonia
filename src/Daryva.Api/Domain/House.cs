using Daryva.Api.Domain.Interfaces;

namespace Daryva.Api.Domain;

/// <summary>
/// Represents a property (house/rental unit) owned by an organization.
/// Implements IOrgScopedEntity for automatic OrgId isolation via global query filters.
/// </summary>
public class House : IOrgScopedEntity
{
    /// <summary>
    /// Unique identifier for the house.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// FK: Organization that owns this property.
    /// CRITICAL: Global query filters enforce isolation by this field.
    /// </summary>
    public Guid OrganizationId { get; set; }

    /// <summary>
    /// Property name/identifier (e.g., "Main St Apartment A").
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Street address line 1.
    /// </summary>
    public required string AddressLine1 { get; set; }

    /// <summary>
    /// Street address line 2 (optional).
    /// </summary>
    public string? AddressLine2 { get; set; }

    /// <summary>
    /// City.
    /// </summary>
    public required string City { get; set; }

    /// <summary>
    /// Postal code.
    /// </summary>
    public required string Postcode { get; set; }

    /// <summary>
    /// Total number of rentable rooms in this property.
    /// </summary>
    public int TotalRooms { get; set; }

    /// <summary>
    /// Timestamp when property was added to system.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When true, house is archived (soft delete). Archived houses are excluded from default lists.
    /// </summary>
    public bool IsArchived { get; set; }

    /// <summary>
    /// Navigation: Organization reference.
    /// </summary>
    public Organization? Organization { get; set; }
}
