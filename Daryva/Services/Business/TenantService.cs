using Daryva.MVVM.Models;
using Daryva.Services.Data;

namespace Daryva.Services.Business
{
    public class TenantService : ITenantService
    {
        private readonly ITenantRepository _tenantRepository;

        public TenantService(ITenantRepository tenantRepository)
        {
            _tenantRepository = tenantRepository;
        }

        public async Task<IEnumerable<Tenant>> GetAllTenantsAsync(bool includeArchived = false)
        {
            return await _tenantRepository.GetAllTenantsAsync(includeArchived);
        }

        public async Task<Tenant?> GetTenantByIdAsync(int tenantId)
        {
            return await _tenantRepository.GetTenantByIdAsync(tenantId);
        }

        public async Task<Tenant> CreateTenantAsync(Tenant tenant)
        {
            var tenantId = await _tenantRepository.CreateTenantAsync(tenant);
            tenant.TenantId = tenantId;
            return tenant;
        }

        public async Task UpdateTenantAsync(Tenant tenant)
        {
            await _tenantRepository.UpdateTenantAsync(tenant);
        }

        public async Task ArchiveTenantAsync(int tenantId)
        {
            await _tenantRepository.ArchiveTenantAsync(tenantId);
        }

        public async Task<IEnumerable<Tenant>> SearchTenantsAsync(string searchTerm)
        {
            return await _tenantRepository.SearchTenantsAsync(searchTerm);
        }
    }
}
