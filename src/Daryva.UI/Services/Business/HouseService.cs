using Daryva.MVVM.Models;
using Daryva.Services.Data;

namespace Daryva.Services.Business
{
    public class HouseService : IHouseService
    {
        private readonly IHouseRepository _houseRepository;
        private readonly ITenancyRepository _tenancyRepository;

        public HouseService(IHouseRepository houseRepository, ITenancyRepository tenancyRepository)
        {
            _houseRepository = houseRepository;
            _tenancyRepository = tenancyRepository ?? throw new ArgumentNullException(nameof(tenancyRepository));
        }

        public async Task<IEnumerable<House>> GetAllHousesAsync(bool includeArchived = false)
        {
            // Local repo does not filter by archived; return all
            return await _houseRepository.GetAllHousesAsync();
        }

        public async Task<House?> ArchiveHouseAsync(int houseId)
        {
            // Archive not implemented for local SQLite; return null (UI may show message)
            return await Task.FromResult<House?>(null);
        }

        public async Task<House?> GetHouseByIdAsync(int houseId)
        {
            return await _houseRepository.GetHouseByIdAsync(houseId);
        }

        public async Task<House> CreateHouseAsync(House house)
        {
            var houseId = await _houseRepository.CreateHouseAsync(house);
            house.HouseId = houseId;
            return house;
        }

        public async Task UpdateHouseAsync(House house)
        {
            await _houseRepository.UpdateHouseAsync(house);
        }

        public async Task DeleteHouseAsync(int houseId)
        {
            // Check if house has any ACTIVE tenancies
            var tenancies = await _tenancyRepository.GetTenanciesByHouseIdAsync(houseId);
            var activeTenancies = tenancies.Where(t => t.Status == "Active").ToList();
            
            if (activeTenancies.Any())
            {
                var count = activeTenancies.Count;
                throw new InvalidOperationException(
                    $"Cannot delete house because it has {count} active tenanc{(count == 1 ? "y" : "ies")}. " +
                    "Please end all active tenancies first.");
            }

            // Delete any ended tenancies (historical records) before deleting the house
            // This is safe because they're just historical data and the house is being deleted anyway
            await _tenancyRepository.DeleteEndedTenanciesByHouseIdAsync(houseId);

            // Now safe to delete the house
            await _houseRepository.DeleteHouseAsync(houseId);
        }

        public async Task<bool> HasTenanciesAsync(int houseId)
        {
            var tenancies = await _tenancyRepository.GetTenanciesByHouseIdAsync(houseId);
            // Only return true if there are ACTIVE tenancies
            return tenancies.Any(t => t.Status == "Active");
        }

        public async Task<IEnumerable<House>> SearchHousesAsync(string searchTerm)
        {
            return await _houseRepository.SearchHousesAsync(searchTerm);
        }
    }
}
