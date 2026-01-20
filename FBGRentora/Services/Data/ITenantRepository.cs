using FBGRentora.MVVM.Models;

namespace FBGRentora.Services.Data
{
    public interface ITenantRepository
    {
        Task<IEnumerable<Tenant>> GetAllTenantsAsync(bool includeArchived = false);
        Task<Tenant?> GetTenantByIdAsync(int tenantId);
        Task<int> CreateTenantAsync(Tenant tenant);
        Task UpdateTenantAsync(Tenant tenant);
        Task ArchiveTenantAsync(int tenantId);
        Task<IEnumerable<Tenant>> SearchTenantsAsync(string searchTerm);
    }
}
