using Daryva.MVVM.Models;
using Daryva.Services.Api;
using Daryva.Services.OrgContext;

namespace Daryva.Services.Business;

/// <summary>
/// Implements ITenantService against the backend API.
/// Maps between UI Tenant model and API TenantDto.
/// </summary>
public class TenantService : ITenantService
{
    private readonly ITenantApiService _tenantApiService;
    private readonly IApiEntityIdMapper _idMapper;
    private readonly IOrgContext _orgContext;

    // Same rationale as HouseService's cache: GetAllTenantsAsync is independently called by
    // Dashboard, Houses, Tenants, Transactions and Notifications on their own page load, so a
    // short cache here cuts redundant re-fetches across all of them at once.
    private static readonly Dictionary<(Guid OrgId, bool IncludeArchived), (DateTime CachedAtUtc, List<Tenant> Tenants)> _cache = new();
    private static readonly object _cacheLock = new();
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(20);

    public TenantService(ITenantApiService tenantApiService, IApiEntityIdMapper idMapper, IOrgContext orgContext)
    {
        _tenantApiService = tenantApiService ?? throw new ArgumentNullException(nameof(tenantApiService));
        _idMapper = idMapper ?? throw new ArgumentNullException(nameof(idMapper));
        _orgContext = orgContext ?? throw new ArgumentNullException(nameof(orgContext));
    }

    public async Task<IEnumerable<Tenant>> GetAllTenantsAsync(bool includeArchived = false)
    {
        var orgId = _orgContext.CurrentOrgId;
        var cacheKey = orgId.HasValue ? (orgId.Value, includeArchived) : default((Guid, bool)?);
        if (cacheKey.HasValue)
        {
            lock (_cacheLock)
            {
                if (_cache.TryGetValue(cacheKey.Value, out var entry) && DateTime.UtcNow - entry.CachedAtUtc < CacheTtl)
                    return entry.Tenants;
            }
        }

        var tenantDtos = await _tenantApiService.GetTenantsAsync(includeArchived);
        var tenants = tenantDtos.Select(MapToTenant).ToList();

        if (cacheKey.HasValue)
        {
            lock (_cacheLock)
            {
                _cache[cacheKey.Value] = (DateTime.UtcNow, tenants);
            }
        }

        return tenants;
    }

    /// <summary>Clears the cached tenant lists for every org -- called after any write, so the
    /// next GetAllTenantsAsync always reflects it instead of serving up to 20s of stale data.</summary>
    private static void InvalidateCache()
    {
        lock (_cacheLock)
        {
            _cache.Clear();
        }
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
            PhoneNumber = tenant.PhoneNumber,
            UniversityName = tenant.UniversityName
        };

        var createdDto = await _tenantApiService.CreateTenantAsync(createDto);
        InvalidateCache();
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
            PhoneNumber = tenant.PhoneNumber,
            UniversityName = tenant.UniversityName
        };

        var updatedDto = await _tenantApiService.UpdateTenantAsync(tenant.ApiId.Value, updateDto);
        
        // Update the tenant object with response data
        tenant.FullName = updatedDto.FullName;
        tenant.Email = updatedDto.Email ?? string.Empty;
        tenant.PhoneNumber = updatedDto.PhoneNumber ?? string.Empty;
        tenant.UniversityName = updatedDto.UniversityName;
        tenant.CreatedAt = updatedDto.CreatedAt;
        tenant.IsArchived = updatedDto.IsArchived;
        InvalidateCache();
    }

    public async Task ArchiveTenantAsync(int tenantId)
    {
        var tenant = await GetTenantByIdAsync(tenantId);
        if (tenant == null || !tenant.ApiId.HasValue)
            throw new InvalidOperationException($"Tenant with ID {tenantId} not found or has no API ID.");

        var archived = await _tenantApiService.ArchiveTenantAsync(tenant.ApiId.Value);
        if (!archived)
            throw new InvalidOperationException($"Failed to archive tenant with ID {tenantId}.");
        InvalidateCache();
    }

    public async Task UnarchiveTenantAsync(int tenantId)
    {
        var tenant = await GetTenantByIdAsync(tenantId);
        if (tenant == null || !tenant.ApiId.HasValue)
            throw new InvalidOperationException($"Tenant with ID {tenantId} not found or has no API ID.");

        var unarchived = await _tenantApiService.UnarchiveTenantAsync(tenant.ApiId.Value);
        if (!unarchived)
            throw new InvalidOperationException($"Failed to unarchive tenant with ID {tenantId}.");
        InvalidateCache();
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
        InvalidateCache();
    }

    public async Task InviteTenantAsync(int tenantId)
    {
        var tenant = await GetTenantByIdAsync(tenantId);
        if (tenant == null || !tenant.ApiId.HasValue)
            throw new InvalidOperationException($"Tenant with ID {tenantId} not found or has no API ID.");

        var invited = await _tenantApiService.InviteTenantAsync(tenant.ApiId.Value);
        if (!invited)
            throw new InvalidOperationException($"Tenant with ID {tenantId} not found.");
    }

    /// <summary>
    /// Map TenantDto from API to UI Tenant model.
    /// Assigns a local int ID based on the hash of the Guid.
    /// </summary>
    private Tenant MapToTenant(TenantDto dto)
    {
        return new Tenant
        {
            TenantId = _idMapper.MapTenantId(dto.Id),
            ApiId = dto.Id,
            FullName = dto.FullName,
            Email = dto.Email ?? string.Empty,
            PhoneNumber = dto.PhoneNumber ?? string.Empty,
            UniversityName = dto.UniversityName,
            CreatedAt = dto.CreatedAt,
            IsArchived = dto.IsArchived,
            CurrentHouseAddress = dto.CurrentHouseAddress,
            CurrentHouseId = dto.CurrentHouseId.HasValue ? (int?)_idMapper.MapHouseId(dto.CurrentHouseId.Value) : null,
            CurrentTenancyId = dto.CurrentTenancyId.HasValue ? (int?)_idMapper.MapTenancyId(dto.CurrentTenancyId.Value) : null,
            LeaveDate = dto.LeaveDate,
            PortalAppUserId = dto.AppUserId,
            PortalInviteSentAt = dto.InviteSentAt,
            PortalInviteAcceptedAt = dto.InviteAcceptedAt
        };
    }
}
