using Daryva.MVVM.Models;

namespace Daryva.Services.Data
{
    public interface ITenancyRepository
    {
        Task<IEnumerable<Tenancy>> GetTenanciesByHouseIdAsync(int houseId);
        Task<IEnumerable<Tenancy>> GetTenanciesByTenantIdAsync(int tenantId);
        Task<Tenancy?> GetTenancyByIdAsync(int tenancyId);
        Task<IEnumerable<Tenancy>> GetActiveTenanciesAsync();
        Task<int> CreateTenancyAsync(Tenancy tenancy);
        Task UpdateTenancyAsync(Tenancy tenancy);
        Task EndTenancyAsync(int tenancyId, DateTime moveOutDate);
    }
}
