namespace Daryva.Services.Api;

/// <summary>
/// DTO for Tenant API responses.
/// </summary>
public class TenantDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? UniversityName { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsArchived { get; set; }
    
    // House/Tenancy information
    public string? CurrentHouseAddress { get; set; }
    public Guid? CurrentTenancyId { get; set; }
    public Guid? CurrentHouseId { get; set; }
    public DateTime? LeaveDate { get; set; }
}

/// <summary>
/// DTO for creating a new tenant.
/// </summary>
public class CreateTenantDto
{
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? UniversityName { get; set; }
}

/// <summary>
/// DTO for updating a tenant.
/// </summary>
public class UpdateTenantDto
{
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? UniversityName { get; set; }
}

/// <summary>
/// Service for tenant-related API operations.
/// </summary>
public interface ITenantApiService
{
    /// <summary>
    /// Get all tenants for the current organization.
    /// </summary>
    Task<List<TenantDto>> GetTenantsAsync(bool includeArchived = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a specific tenant by ID.
    /// </summary>
    Task<TenantDto?> GetTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a new tenant in the current organization.
    /// </summary>
    Task<TenantDto> CreateTenantAsync(CreateTenantDto tenant, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update an existing tenant.
    /// </summary>
    Task<TenantDto> UpdateTenantAsync(Guid tenantId, UpdateTenantDto tenant, CancellationToken cancellationToken = default);

    /// <summary>
    /// Archive a tenant.
    /// </summary>
    Task<bool> ArchiveTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Unarchive a tenant.
    /// </summary>
    Task<bool> UnarchiveTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a tenant.
    /// </summary>
    Task<bool> DeleteTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
