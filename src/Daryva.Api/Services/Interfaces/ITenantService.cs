using Daryva.Api.Dtos;

namespace Daryva.Api.Services.Interfaces;

/// <summary>
/// Business logic for tenant management.
/// All operations are automatically filtered by current organization via global query filters.
/// </summary>
public interface ITenantService
{
    /// <summary>
    /// Get all tenants for the current organization.
    /// </summary>
    Task<List<TenantResponse>> GetAllTenantsAsync(
        bool includeArchived = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a specific tenant by ID (must belong to current org).
    /// </summary>
    Task<TenantResponse?> GetTenantByIdAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a new tenant. Throws ArgumentException if the request is invalid.
    /// </summary>
    Task<TenantResponse> CreateTenantAsync(
        CreateTenantRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Update an existing tenant (if it belongs to current org). Null return means not found.
    /// </summary>
    Task<TenantResponse?> UpdateTenantAsync(
        Guid tenantId,
        UpdateTenantRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a tenant (if it belongs to current org).
    /// </summary>
    Task DeleteTenantAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Archive a tenant and end any active tenancies (tracked, single save).
    /// </summary>
    Task<bool> ArchiveTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Unarchive a tenant.
    /// </summary>
    Task<bool> UnarchiveTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
