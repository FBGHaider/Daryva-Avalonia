using LandLordBuddy.MVVM.Models;

namespace LandLordBuddy.Services.Business
{
    public interface IHouseService
    {
        Task<IEnumerable<House>> GetAllHousesAsync();
        Task<House?> GetHouseByIdAsync(int houseId);
        Task<House> CreateHouseAsync(House house);
        Task UpdateHouseAsync(House house);
        Task DeleteHouseAsync(int houseId);
        Task<IEnumerable<House>> SearchHousesAsync(string searchTerm);
    }
}
