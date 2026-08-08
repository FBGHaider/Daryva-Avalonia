using Daryva.MVVM.Models;
using Daryva.Services.Api;
using Daryva.Services.OrgContext;

namespace Daryva.Services.Business;

/// <summary>
/// Implements IHouseService against the backend API.
/// Maps between UI House model and API HouseDto.
/// </summary>
public class HouseService : IHouseService
{
    private readonly IHouseApiService _houseApiService;
    private readonly ITenantApiService _tenantApiService;
    private readonly IApiEntityIdMapper _idMapper;
    private readonly IOrgContext _orgContext;

    // GetAllHousesAsync is the single most-called read in the app -- Dashboard, Houses, Tenants,
    // Rent Ledger, Transactions and Expenses each call it independently on their own page load, so
    // switching between them repeatedly re-fetches the identical house list over and over. A short
    // cache here benefits every one of those callers at once instead of needing its own cache in
    // each ViewModel. Confirmed via the session log that this redundancy was a real contributor to
    // rate-limit (429) storms under rapid tab switching.
    private static readonly Dictionary<(Guid OrgId, bool IncludeArchived), (DateTime CachedAtUtc, List<House> Houses)> _cache = new();
    private static readonly object _cacheLock = new();
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(20);

    public HouseService(
        IHouseApiService houseApiService,
        ITenantApiService tenantApiService,
        IApiEntityIdMapper idMapper,
        IOrgContext orgContext)
    {
        _houseApiService = houseApiService ?? throw new ArgumentNullException(nameof(houseApiService));
        _tenantApiService = tenantApiService ?? throw new ArgumentNullException(nameof(tenantApiService));
        _idMapper = idMapper ?? throw new ArgumentNullException(nameof(idMapper));
        _orgContext = orgContext ?? throw new ArgumentNullException(nameof(orgContext));
    }

    public async Task<IEnumerable<House>> GetAllHousesAsync(bool includeArchived = false)
    {
        var orgId = _orgContext.CurrentOrgId;
        var cacheKey = orgId.HasValue ? (orgId.Value, includeArchived) : default((Guid, bool)?);
        if (cacheKey.HasValue)
        {
            lock (_cacheLock)
            {
                if (_cache.TryGetValue(cacheKey.Value, out var entry) && DateTime.UtcNow - entry.CachedAtUtc < CacheTtl)
                    return entry.Houses;
            }
        }

        // Neither call depends on the other's result -- fetching them one at a time (as before)
        // doubles this method's latency for nothing. Every visit to the Houses tab goes through
        // here, so under slower network conditions this was a real, measurable contributor to
        // the multi-second "Loading houses" delays reported for that page.
        var housesTask = _houseApiService.GetHousesAsync(includeArchived);
        var tenantsTask = _tenantApiService.GetTenantsAsync(includeArchived: false);
        await Task.WhenAll(housesTask, tenantsTask);

        var houseDtos = housesTask.Result;
        var activeTenants = tenantsTask.Result;

        var activeTenantCountsByHouse = activeTenants
            .Where(t => !t.IsArchived && t.CurrentHouseId.HasValue)
            .GroupBy(t => t.CurrentHouseId!.Value)
            .ToDictionary(
                g => g.Key,
                g => g.Select(t => t.Id).Distinct().Count());

        var houses = houseDtos.Select(dto =>
        {
            var house = MapToHouse(dto);
            if (house.ApiId.HasValue && activeTenantCountsByHouse.TryGetValue(house.ApiId.Value, out var count))
            {
                house.ActiveTenantCount = count;
            }
            else
            {
                house.ActiveTenantCount = 0;
            }

            return house;
        }).ToList();

        if (cacheKey.HasValue)
        {
            lock (_cacheLock)
            {
                _cache[cacheKey.Value] = (DateTime.UtcNow, houses);
            }
        }

        return houses;
    }

    /// <summary>Clears the cached house lists for every org -- called after any write, so the
    /// next GetAllHousesAsync always reflects it instead of serving up to 20s of stale data.</summary>
    private static void InvalidateCache()
    {
        lock (_cacheLock)
        {
            _cache.Clear();
        }
    }

    public async Task<House?> ArchiveHouseAsync(int houseId)
    {
        var house = await GetHouseByIdAsync(houseId);
        if (house == null || !house.ApiId.HasValue)
            return null;

        var dto = await _houseApiService.ArchiveHouseAsync(house.ApiId.Value);
        InvalidateCache();
        return dto != null ? MapToHouse(dto) : null;
    }

    public async Task<House?> GetHouseByIdAsync(int houseId)
    {
        // In API mode, we need to fetch all houses and find by local ID
        // This is not ideal, but the UI uses int IDs while API uses Guids
        var houses = await GetAllHousesAsync();
        return houses.FirstOrDefault(h => h.HouseId == houseId);
    }

    public async Task<House> CreateHouseAsync(House house)
    {
        var createDto = new CreateHouseDto
        {
            Name = string.IsNullOrWhiteSpace(house.Name) ? house.AddressLine1 : house.Name,
            AddressLine1 = house.AddressLine1,
            AddressLine2 = house.AddressLine2,
            City = house.City,
            Postcode = house.Postcode,
            TotalRooms = house.TotalRooms
        };

        var createdDto = await _houseApiService.CreateHouseAsync(createDto);
        InvalidateCache();
        return MapToHouse(createdDto);
    }

    public async Task UpdateHouseAsync(House house)
    {
        if (!house.ApiId.HasValue)
            throw new InvalidOperationException("Cannot update house without API ID.");

        var updateDto = new UpdateHouseDto
        {
            Name = house.Name,
            AddressLine1 = house.AddressLine1,
            AddressLine2 = house.AddressLine2,
            City = house.City,
            Postcode = house.Postcode,
            TotalRooms = house.TotalRooms
        };

        var updatedDto = await _houseApiService.UpdateHouseAsync(house.ApiId.Value, updateDto);
        
        // Update the house object with response data
        house.Name = updatedDto.Name;
        house.AddressLine1 = updatedDto.AddressLine1;
        house.AddressLine2 = updatedDto.AddressLine2;
        house.City = updatedDto.City;
        house.Postcode = updatedDto.Postcode;
        house.TotalRooms = updatedDto.TotalRooms;
        house.CreatedAt = updatedDto.CreatedAt;
        InvalidateCache();
    }

    public async Task DeleteHouseAsync(int houseId)
    {
        // Find the house to get its API ID
        var house = await GetHouseByIdAsync(houseId);
        if (house == null || !house.ApiId.HasValue)
            throw new InvalidOperationException($"House with ID {houseId} not found or has no API ID.");

        var deleted = await _houseApiService.DeleteHouseAsync(house.ApiId.Value);
        if (!deleted)
            throw new InvalidOperationException($"Failed to delete house with ID {houseId}.");
        InvalidateCache();
    }

    public async Task<IEnumerable<House>> SearchHousesAsync(string searchTerm)
    {
        // Get all houses and filter client-side
        // TODO: Backend should support search endpoint for better performance
        var allHouses = await GetAllHousesAsync();
        
        if (string.IsNullOrWhiteSpace(searchTerm))
            return allHouses;

        var lowerSearchTerm = searchTerm.ToLowerInvariant();
        return allHouses.Where(h =>
            h.Name.ToLowerInvariant().Contains(lowerSearchTerm) ||
            h.AddressLine1.ToLowerInvariant().Contains(lowerSearchTerm) ||
            (h.AddressLine2?.ToLowerInvariant().Contains(lowerSearchTerm) ?? false) ||
            h.City.ToLowerInvariant().Contains(lowerSearchTerm) ||
            h.Postcode.ToLowerInvariant().Contains(lowerSearchTerm));
    }

    public async Task<bool> HasTenanciesAsync(int houseId)
    {
        // TODO: Backend needs endpoint to check for active tenancies
        // For now, return false (allow deletion)
        // The backend will reject deletion if there are active tenancies
        return await Task.FromResult(false);
    }

    /// <summary>
    /// Map HouseDto from API to UI House model.
    /// Assigns a local int ID based on the hash of the Guid.
    /// </summary>
    private House MapToHouse(HouseDto dto)
    {
        return new House
        {
            HouseId = _idMapper.MapHouseId(dto.Id),
            ApiId = dto.Id,
            Name = dto.Name,
            AddressLine1 = dto.AddressLine1,
            AddressLine2 = dto.AddressLine2,
            City = dto.City,
            Postcode = dto.Postcode,
            CreatedAt = dto.CreatedAt,
            TotalRooms = dto.TotalRooms,
            ActiveTenantCount = dto.ActiveTenantCount,
            TotalMonthlyRent = dto.TotalMonthlyRent,
            IsArchived = dto.IsArchived
        };
    }
}
