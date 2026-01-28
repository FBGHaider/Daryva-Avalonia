using Daryva.MVVM.Models;

namespace Daryva.Services.Data
{
    public interface IDepositPaymentRepository
    {
        Task<IEnumerable<DepositPayment>> GetDepositPaymentsByTenancyIdAsync(int tenancyId);
        Task<decimal> GetTotalDepositPaidAsync(int tenancyId);
        Task<int> CreateDepositPaymentAsync(DepositPayment payment);
        Task<IEnumerable<DepositPayment>> GetAllDepositPaymentsAsync(DateTime? startDate = null, DateTime? endDate = null, int? tenancyId = null);
        Task<bool> DeleteDepositPaymentAsync(int depositPaymentId);
        Task DeleteDepositPaymentsByTenancyIdAsync(int tenancyId);
        Task<DepositPayment?> GetDepositPaymentByIdAsync(int depositPaymentId);
    }
}
