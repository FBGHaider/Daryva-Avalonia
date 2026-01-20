using LandLordBuddy.MVVM.Models;
using LandLordBuddy.Services.Data;

namespace LandLordBuddy.Services.Business
{
    public class HouseService : IHouseService
    {
        private readonly IHouseRepository _houseRepository;

        public HouseService(IHouseRepository houseRepository)
        {
            _houseRepository = houseRepository;
        }

        public async Task<IEnumerable<House>> GetAllHousesAsync()
        {
            return await _houseRepository.GetAllHousesAsync();
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
            await _houseRepository.DeleteHouseAsync(houseId);
        }

        public async Task<IEnumerable<House>> SearchHousesAsync(string searchTerm)
        {
            return await _houseRepository.SearchHousesAsync(searchTerm);
        }
    }
}
