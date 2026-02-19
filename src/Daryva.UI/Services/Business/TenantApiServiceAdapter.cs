using Daryva.MVVM.Models;
using Daryva.Services.Api;

namespace Daryva.Services.Business;

/// <summary>
/// Adapter that implements ITenantService using the backend API.
/// Maps between UI Tenant model and API TenantDto.
/// Replaces the SQLite-based TenantService when using API backend.
/// </summary>
public class TenantApiServiceAdapter : ITenantService
{
    private readonly ITenantApiService _tenantApiService;

    public TenantApiServiceAdapter(ITenantApiService tenantApiService)
    {
        _tenantApiService = tenantApiService ?? throw new ArgumentNullException(nameof(tenantApiService));
    }

    public async Task<IEnumerable<Tenant>> GetAllTenantsAsync(bool includeArchived = false)
    {
        var tenantDtos = await _tenantApiService.GetTenantsAsync(includeArchived);
        return tenantDtos.Select(MapToTenant);
    }

    public async Task<IEnumerable<Tenant>> GetTenantsByHouseIdAsync(int? houseId, bool includeArchived = false)
    {
        var allTenants = await GetAllTenantsAsync(includeArchived);

        if (!houseId.HasValue)
            return allTenants;

        return allTenants.Where(t => t.CurrentHouseId == houseId.Value);
    }

    public async Task<Tenant?> GetTenantByIdAsync(int tenantId)
    {
        // In API mode, we need to fetch all tenants and find by local ID
        var tenants = await GetAllTenantsAsync(includeArchived: true);
        return tenants.FirstOrDefault(t => t.TenantId == tenantId);
    }

    public async Task<Tenant> CreateTenantAsync(Tenant tenant)
    {
        var createDto = new CreateTenantDto
        {
            FullName = tenant.FullName,
            Email = tenant.Email,
            PhoneNumber = tenant.PhoneNumber
        };

        var createdDto = await _tenantApiService.CreateTenantAsync(createDto);
        return MapToTenant(createdDto);
    }

    public async Task UpdateTenantAsync(Tenant tenant)
    {
        if (!tenant.ApiId.HasValue)
            throw new InvalidOperationException("Cannot update tenant without API ID.");

        var updateDto = new UpdateTenantDto
        {
            FullName = tenant.FullName,
            Email = tenant.Email,
            PhoneNumber = tenant.PhoneNumber
        };

        var updatedDto = await _tenantApiService.UpdateTenantAsync(tenant.ApiId.Value, updateDto);
        
        // Update the tenant object with response data
        tenant.FullName = updatedDto.FullName;
        tenant.Email = updatedDto.Email ?? string.Empty;
        tenant.PhoneNumber = updatedDto.PhoneNumber ?? string.Empty;
        tenant.CreatedAt = updatedDto.CreatedAt;
        tenant.IsArchived = updatedDto.IsArchived;
    }

    public async Task ArchiveTenantAsync(int tenantId)
    {
        var tenant = await GetTenantByIdAsync(tenantId);
        if (tenant == null || !tenant.ApiId.HasValue)
            throw new InvalidOperationException($"Tenant with ID {tenantId} not found or has no API ID.");

        var archived = await _tenantApiService.ArchiveTenantAsync(tenant.ApiId.Value);
        if (!archived)
            throw new InvalidOperationException($"Failed to archive tenant with ID {tenantId}.");
    }

    public async Task UnarchiveTenantAsync(int tenantId)
    {
        var tenant = await GetTenantByIdAsync(tenantId);
        if (tenant == null || !tenant.ApiId.HasValue)
            throw new InvalidOperationException($"Tenant with ID {tenantId} not found or has no API ID.");

        var unarchived = await _tenantApiService.UnarchiveTenantAsync(tenant.ApiId.Value);
        if (!unarchived)
            throw new InvalidOperationException($"Failed to unarchive tenant with ID {tenantId}.");
    }

    public async Task<IEnumerable<Tenant>> SearchTenantsAsync(string searchTerm)
    {
        // Get all tenants and filter client-side
        var allTenants = await GetAllTenantsAsync();
        
        if (string.IsNullOrWhiteSpace(searchTerm))
            return allTenants;

        var lowerSearchTerm = searchTerm.ToLowerInvariant();
        return allTenants.Where(t =>
            (t.FullName?.ToLowerInvariant().Contains(lowerSearchTerm) ?? false) ||
            (t.Email?.ToLowerInvariant().Contains(lowerSearchTerm) ?? false) ||
            (t.PhoneNumber?.ToLowerInvariant().Contains(lowerSearchTerm) ?? false) ||
            (t.UniversityName?.ToLowerInvariant().Contains(lowerSearchTerm) ?? false));
    }

    public async Task DeleteTenantAsync(int tenantId)
    {
        var tenant = await GetTenantByIdAsync(tenantId);
        if (tenant == null || !tenant.ApiId.HasValue)
            throw new InvalidOperationException($"Tenant with ID {tenantId} not found or has no API ID.");

        var deleted = await _tenantApiService.DeleteTenantAsync(tenant.ApiId.Value);
        if (!deleted)
            throw new InvalidOperationException($"Failed to delete tenant with ID {tenantId}.");
    }

    /// <summary>
    /// Map TenantDto from API to UI Tenant model.
    /// Assigns a local int ID based on the hash of the Guid.
    /// </summary>
    private Tenant MapToTenant(TenantDto dto)
    {
        return new Tenant
        {
            TenantId = dto.Id.GetHashCode(),
            ApiId = dto.Id,
            FullName = dto.FullName,
            Email = dto.Email ?? string.Empty,
            PhoneNumber = dto.PhoneNumber ?? string.Empty,
            UniversityName = dto.UniversityName,
            CreatedAt = dto.CreatedAt,
            IsArchived = dto.IsArchived,
            CurrentHouseAddress = dto.CurrentHouseAddress,
            CurrentHouseId = dto.CurrentHouseId.HasValue ? (int?)dto.CurrentHouseId.Value.GetHashCode() : null,
            CurrentTenancyId = dto.CurrentTenancyId.HasValue ? (int?)dto.CurrentTenancyId.Value.GetHashCode() : null,
            LeaveDate = null
        };
    }
}
