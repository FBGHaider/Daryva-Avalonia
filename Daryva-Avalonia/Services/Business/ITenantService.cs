using Daryva.MVVM.Models;

namespace Daryva.Services.Business
{
    public interface ITenantService
    {
        Task<IEnumerable<Tenant>> GetAllTenantsAsync(bool includeArchived = false);
        /// <summary>When houseId is null, returns all tenants; otherwise only tenants at that house.</summary>
        Task<IEnumerable<Tenant>> GetTenantsByHouseIdAsync(int? houseId, bool includeArchived = false);
        Task<Tenant?> GetTenantByIdAsync(int tenantId);
        Task<Tenant> CreateTenantAsync(Tenant tenant);
        Task UpdateTenantAsync(Tenant tenant);
        Task ArchiveTenantAsync(int tenantId);
        Task UnarchiveTenantAsync(int tenantId);
        Task<IEnumerable<Tenant>> SearchTenantsAsync(string searchTerm);
        Task DeleteTenantAsync(int tenantId);
    }
}
