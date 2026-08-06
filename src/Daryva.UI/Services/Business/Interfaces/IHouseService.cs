using Daryva.MVVM.Models;

namespace Daryva.Services.Business
{
    public interface IHouseService
    {
    Task<IEnumerable<House>> GetAllHousesAsync(bool includeArchived = false);
    Task<House?> GetHouseByIdAsync(int houseId);
    Task<House?> ArchiveHouseAsync(int houseId);
        Task<House> CreateHouseAsync(House house);
        Task UpdateHouseAsync(House house);
        Task DeleteHouseAsync(int houseId);
        Task<IEnumerable<House>> SearchHousesAsync(string searchTerm);
        Task<bool> HasTenanciesAsync(int houseId);
    }
}
