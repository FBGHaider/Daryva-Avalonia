using Daryva.Api.Dtos;

namespace Daryva.Api.Services.Interfaces;

/// <summary>
/// Business logic for house (property) management.
/// All operations are automatically filtered by current organization via global query filters.
/// </summary>
public interface IHouseService
{
    /// <summary>
    /// Get all houses for the current organization.
    /// </summary>
    /// <param name="includeArchived">When false, archived houses are excluded.</param>
    Task<IEnumerable<HouseResponse>> GetHousesAsync(
        Guid orgId,
        bool includeArchived = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Archive a house (soft delete). Idempotent if already archived.
    /// </summary>
    Task<HouseResponse?> ArchiveHouseAsync(
        Guid orgId,
        Guid houseId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a specific house by ID (must belong to current org).
    /// </summary>
    Task<HouseResponse?> GetHouseAsync(
        Guid orgId,
        Guid houseId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a new house. OrganizationId is set server-side (ignore client input).
    /// </summary>
    Task<HouseResponse> CreateHouseAsync(
        Guid orgId,
        CreateHouseRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Update an existing house (if it belongs to current org).
    /// </summary>
    Task<HouseResponse?> UpdateHouseAsync(
        Guid orgId,
        Guid houseId,
        UpdateHouseRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a house (if it belongs to current org).
    /// </summary>
    Task<bool> DeleteHouseAsync(
        Guid orgId,
        Guid houseId,
        CancellationToken cancellationToken = default);
}
