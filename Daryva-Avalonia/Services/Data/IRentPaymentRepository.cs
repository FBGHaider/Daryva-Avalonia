using Daryva.MVVM.Models;

namespace Daryva.Services.Data
{
    public interface IRentPaymentRepository
    {
        Task<IEnumerable<RentPayment>> GetRentPaymentsByChargeIdAsync(int rentChargeId);
        Task<decimal> GetTotalRentPaidForChargeAsync(int rentChargeId);
        Task<int> CreateRentPaymentAsync(RentPayment payment);
        Task<IEnumerable<RentPayment>> GetAllRentPaymentsAsync(DateTime? startDate = null, DateTime? endDate = null, int? tenancyId = null);
        Task<bool> DeleteRentPaymentAsync(int rentPaymentId);
        Task DeleteRentPaymentsByTenancyIdAsync(int tenancyId);
        Task<RentPayment?> GetRentPaymentByIdAsync(int rentPaymentId);
        /// <summary>Reassigns all payments from one charge to another (for merging duplicate charges).</summary>
        Task<int> ReassignPaymentsToChargeAsync(int fromRentChargeId, int toRentChargeId);
    }
}
