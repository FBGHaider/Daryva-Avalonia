using Daryva.MVVM.Models;

namespace Daryva.Services.Data
{
    public interface ITenancyRepository
    {
        Task<IEnumerable<Tenancy>> GetTenanciesByHouseIdAsync(int houseId);
        Task<IEnumerable<Tenancy>> GetTenanciesByTenantIdAsync(int tenantId);
        Task<Tenancy?> GetTenancyByIdAsync(int tenancyId);
        Task<IEnumerable<Tenancy>> GetActiveTenanciesAsync();
        /// <summary>Tenancies that were active at some point during the given month (includes ended tenancies and archived tenants).</summary>
        Task<IEnumerable<Tenancy>> GetTenanciesActiveInPeriodAsync(int year, int month);
        Task<Dictionary<int, (int Month, int Year)>> GetRentStartByTenancyIdsAsync(IEnumerable<int> tenancyIds);
        Task<int> CreateTenancyAsync(Tenancy tenancy);
        Task UpdateTenancyAsync(Tenancy tenancy);
        Task EndTenancyAsync(int tenancyId, DateTime moveOutDate);
        /// <summary>Sets Status = 'Active' and MoveOutDate = null for the given tenancy (e.g. when recovering a tenant).</summary>
        Task ReactivateTenancyAsync(int tenancyId);
        Task<IEnumerable<Tenancy>> GetEndedTenanciesWithDepositAsync();
        Task DeleteTenancyAsync(int tenancyId);
        Task DeleteEndedTenanciesByHouseIdAsync(int houseId);
    }
}
