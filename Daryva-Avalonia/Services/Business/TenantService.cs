using Daryva.MVVM.Models;
using Daryva.Services.Data;

namespace Daryva.Services.Business
{
    public class TenantService : ITenantService
    {
        private readonly ITenantRepository _tenantRepository;
        private readonly ITenancyRepository _tenancyRepository;
        private readonly INotificationRepository _notificationRepository;
        private readonly IRentPaymentRepository _rentPaymentRepository;
        private readonly IDepositPaymentRepository _depositPaymentRepository;
        private readonly IRentChargeRepository _rentChargeRepository;
        private readonly IDocumentRepository _documentRepository;

        public TenantService(
            ITenantRepository tenantRepository, 
            ITenancyRepository tenancyRepository,
            INotificationRepository notificationRepository,
            IRentPaymentRepository rentPaymentRepository,
            IDepositPaymentRepository depositPaymentRepository,
            IRentChargeRepository rentChargeRepository,
            IDocumentRepository documentRepository)
        {
            _tenantRepository = tenantRepository;
            _tenancyRepository = tenancyRepository ?? throw new ArgumentNullException(nameof(tenancyRepository));
            _notificationRepository = notificationRepository ?? throw new ArgumentNullException(nameof(notificationRepository));
            _rentPaymentRepository = rentPaymentRepository ?? throw new ArgumentNullException(nameof(rentPaymentRepository));
            _depositPaymentRepository = depositPaymentRepository ?? throw new ArgumentNullException(nameof(depositPaymentRepository));
            _rentChargeRepository = rentChargeRepository ?? throw new ArgumentNullException(nameof(rentChargeRepository));
            _documentRepository = documentRepository ?? throw new ArgumentNullException(nameof(documentRepository));
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

        public async Task DeleteTenantAsync(int tenantId)
        {
            // Get all tenancies for this tenant
            var tenancies = await _tenancyRepository.GetTenanciesByTenantIdAsync(tenantId);
            var tenancyIds = tenancies.Select(t => t.TenancyId).ToList();

            // Delete in correct order to avoid foreign key constraint violations:
            // 1. Notifications (by tenant ID and by tenancy ID)
            await _notificationRepository.DeleteNotificationsByTenantIdAsync(tenantId);
            foreach (var tenancyId in tenancyIds)
            {
                await _notificationRepository.DeleteNotificationsByTenancyIdAsync(tenancyId);
            }

            // 2. Rent Payments (by tenancy ID - they reference RentCharge)
            foreach (var tenancyId in tenancyIds)
            {
                await _rentPaymentRepository.DeleteRentPaymentsByTenancyIdAsync(tenancyId);
            }

            // 3. Rent Charges (by tenancy ID)
            foreach (var tenancyId in tenancyIds)
            {
                await _rentChargeRepository.DeleteRentChargesByTenancyIdAsync(tenancyId);
            }

            // 4. Deposit Payments (by tenancy ID)
            foreach (var tenancyId in tenancyIds)
            {
                await _depositPaymentRepository.DeleteDepositPaymentsByTenancyIdAsync(tenancyId);
            }

            // 5. Documents (by tenant ID and tenancy ID)
            await _documentRepository.DeleteDocumentsByTenantIdAsync(tenantId);
            foreach (var tenancyId in tenancyIds)
            {
                await _documentRepository.DeleteDocumentsByTenancyIdAsync(tenancyId);
            }

            // 6. Tenancies
            foreach (var tenancy in tenancies)
            {
                await _tenancyRepository.DeleteTenancyAsync(tenancy.TenancyId);
            }

            // 7. Finally, delete the tenant
            await _tenantRepository.DeleteTenantAsync(tenantId);
        }
    }
}
