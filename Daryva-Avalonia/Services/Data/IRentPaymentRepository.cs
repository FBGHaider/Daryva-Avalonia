using Daryva.MVVM.Models;

namespace Daryva.Services.Data
{
    public interface IRentPaymentRepository
    {
        Task<IEnumerable<RentPayment>> GetRentPaymentsByChargeIdAsync(int rentChargeId);
        Task<decimal> GetTotalRentPaidForChargeAsync(int rentChargeId);
        Task<int> CreateRentPaymentAsync(RentPayment payment);
        Task<IEnumerable<RentPayment>> GetAllRentPaymentsAsync(DateTime? startDate = null, DateTime? endDate = null, int? tenancyId = null);
        /// <summary>All rent payments whose PaidOn falls in the given calendar month. Uses date strings so SQLite matches reliably (fixes ledger showing Overdue for left tenants).</summary>
        Task<IEnumerable<RentPayment>> GetRentPaymentsInPeriodAsync(int year, int month);
        /// <summary>Rent payments for a tenancy whose PaidOn falls in the given calendar month.</summary>
        Task<IEnumerable<RentPayment>> GetRentPaymentsForTenancyInPeriodAsync(int tenancyId, int year, int month);
        Task<bool> DeleteRentPaymentAsync(int rentPaymentId);
        Task DeleteRentPaymentsByTenancyIdAsync(int tenancyId);
        Task<RentPayment?> GetRentPaymentByIdAsync(int rentPaymentId);
        /// <summary>Sum of rent payment amounts where PaidFromDeposit=1 for the tenancy (reduces deposit to return).</summary>
        Task<decimal> GetTotalRentPaidFromDepositAsync(int tenancyId);
        /// <summary>Reassigns all payments from one charge to another (for merging duplicate charges).</summary>
        Task<int> ReassignPaymentsToChargeAsync(int fromRentChargeId, int toRentChargeId);
        /// <summary>Updates a single payment's RentChargeId (for repairing unlinked or wrong charge links).</summary>
        Task<bool> UpdateRentChargeIdAsync(int rentPaymentId, int rentChargeId);
    }
}
