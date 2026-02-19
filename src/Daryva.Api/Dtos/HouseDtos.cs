namespace Daryva.Api.Dtos;

/// <summary>
/// Request payload for creating a house.
/// OrganizationId is set server-side from the current org context.
/// </summary>
public class CreateHouseRequest
{
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
}

/// <summary>
/// Request payload for updating a house.
/// </summary>
public class UpdateHouseRequest
{
    /// <summary>
    /// Property name/identifier.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Street address line 1.
    /// </summary>
    public string? AddressLine1 { get; set; }

    /// <summary>
    /// Street address line 2.
    /// </summary>
    public string? AddressLine2 { get; set; }

    /// <summary>
    /// City.
    /// </summary>
    public string? City { get; set; }

    /// <summary>
    /// Postal code.
    /// </summary>
    public string? Postcode { get; set; }
}

/// <summary>
/// Response payload for a house.
/// </summary>
public class HouseResponse
{
    /// <summary>
    /// Unique identifier for the house.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The organization ID (for reference; always matches current context).
    /// </summary>
    public Guid OrganizationId { get; set; }

    /// <summary>
    /// Property name/identifier.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Street address line 1.
    /// </summary>
    public string AddressLine1 { get; set; } = string.Empty;

    /// <summary>
    /// Street address line 2.
    /// </summary>
    public string? AddressLine2 { get; set; }

    /// <summary>
    /// City.
    /// </summary>
    public string City { get; set; } = string.Empty;

    /// <summary>
    /// Postal code.
    /// </summary>
    public string Postcode { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when property was added to system.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Count of active tenants for the house.
    /// </summary>
    public int ActiveTenantCount { get; set; }

    /// <summary>
    /// Sum of monthly rent for active tenancies.
    /// </summary>
    public decimal TotalMonthlyRent { get; set; }
}
