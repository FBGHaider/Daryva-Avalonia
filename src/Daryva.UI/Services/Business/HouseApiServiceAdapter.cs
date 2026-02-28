using Daryva.MVVM.Models;
using Daryva.Services.Api;

namespace Daryva.Services.Business;

/// <summary>
/// Adapter that implements IHouseService using the backend API.
/// Maps between UI House model and API HouseDto.
/// Replaces the SQLite-based HouseService when using API backend.
/// </summary>
public class HouseApiServiceAdapter : IHouseService
{
    private readonly IHouseApiService _houseApiService;
    private readonly ITenantApiService _tenantApiService;
    private readonly IApiEntityIdMapper _idMapper;

    public HouseApiServiceAdapter(
        IHouseApiService houseApiService,
        ITenantApiService tenantApiService,
        IApiEntityIdMapper idMapper)
    {
        _houseApiService = houseApiService ?? throw new ArgumentNullException(nameof(houseApiService));
        _tenantApiService = tenantApiService ?? throw new ArgumentNullException(nameof(tenantApiService));
        _idMapper = idMapper ?? throw new ArgumentNullException(nameof(idMapper));
    }

    public async Task<IEnumerable<House>> GetAllHousesAsync(bool includeArchived = false)
    {
        var houseDtos = await _houseApiService.GetHousesAsync(includeArchived);
        var activeTenants = await _tenantApiService.GetTenantsAsync(includeArchived: false);

        var activeTenantCountsByHouse = activeTenants
            .Where(t => !t.IsArchived && t.CurrentHouseId.HasValue)
            .GroupBy(t => t.CurrentHouseId!.Value)
            .ToDictionary(
                g => g.Key,
                g => g.Select(t => t.Id).Distinct().Count());

        return houseDtos.Select(dto =>
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
        });
    }

    public async Task<House?> ArchiveHouseAsync(int houseId)
    {
        var house = await GetHouseByIdAsync(houseId);
        if (house == null || !house.ApiId.HasValue)
            return null;

        var dto = await _houseApiService.ArchiveHouseAsync(house.ApiId.Value);
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
