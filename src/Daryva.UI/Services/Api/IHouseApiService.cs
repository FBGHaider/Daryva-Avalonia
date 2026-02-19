using System.Text.Json.Serialization;

namespace Daryva.Services.Api;

/// <summary>
/// House data transfer objects matching backend API.
/// These are separate from the UI House model to handle ID type differences (Guid vs int).
/// </summary>
public class HouseDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("organizationId")]
    public Guid OrganizationId { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("addressLine1")]
    public string AddressLine1 { get; set; } = string.Empty;

    [JsonPropertyName("addressLine2")]
    public string? AddressLine2 { get; set; }

    [JsonPropertyName("city")]
    public string City { get; set; } = string.Empty;

    [JsonPropertyName("postcode")]
    public string Postcode { get; set; } = string.Empty;

    [JsonPropertyName("totalRooms")]
    public int TotalRooms { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("activeTenantCount")]
    public int ActiveTenantCount { get; set; }

    [JsonPropertyName("totalMonthlyRent")]
    public decimal TotalMonthlyRent { get; set; }
}

public class CreateHouseDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("addressLine1")]
    public string AddressLine1 { get; set; } = string.Empty;

    [JsonPropertyName("addressLine2")]
    public string? AddressLine2 { get; set; }

    [JsonPropertyName("city")]
    public string City { get; set; } = string.Empty;

    [JsonPropertyName("postcode")]
    public string Postcode { get; set; } = string.Empty;

    [JsonPropertyName("totalRooms")]
    public int TotalRooms { get; set; }
}

public class UpdateHouseDto
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("addressLine1")]
    public string? AddressLine1 { get; set; }

    [JsonPropertyName("addressLine2")]
    public string? AddressLine2 { get; set; }

    [JsonPropertyName("city")]
    public string? City { get; set; }

    [JsonPropertyName("postcode")]
    public string? Postcode { get; set; }

    [JsonPropertyName("totalRooms")]
    public int? TotalRooms { get; set; }
}

/// <summary>
/// Service for house-related API operations.
/// </summary>
public interface IHouseApiService
{
    /// <summary>
    /// Get all houses for the current organization.
    /// Requires X-Org-Id header to be set via IApiClient.
    /// </summary>
    Task<List<HouseDto>> GetHousesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a specific house by ID.
    /// </summary>
    Task<HouseDto?> GetHouseAsync(Guid houseId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a new house in the current organization.
    /// </summary>
    Task<HouseDto> CreateHouseAsync(CreateHouseDto house, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update an existing house.
    /// </summary>
    Task<HouseDto> UpdateHouseAsync(Guid houseId, UpdateHouseDto house, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a house.
    /// </summary>
    Task<bool> DeleteHouseAsync(Guid houseId, CancellationToken cancellationToken = default);
}
