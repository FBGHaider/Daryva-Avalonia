using FBGRentora.MVVM.Models;

namespace FBGRentora.Services.Data
{
    public interface IHouseRepository
    {
        Task<IEnumerable<House>> GetAllHousesAsync();
        Task<House?> GetHouseByIdAsync(int houseId);
        Task<int> CreateHouseAsync(House house);
        Task UpdateHouseAsync(House house);
        Task DeleteHouseAsync(int houseId);
        Task<IEnumerable<House>> SearchHousesAsync(string searchTerm);
    }
}
