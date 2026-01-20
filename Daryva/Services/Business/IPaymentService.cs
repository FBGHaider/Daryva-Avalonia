using Daryva.MVVM.Models;

namespace Daryva.Services.Business
{
    public interface IPaymentService
    {
        Task RecordPaymentAsync(int tenancyId, decimal depositAmount, decimal rentAmount, int rentYear, int rentMonth, DateTime paymentDate, string method, string? reference, string? notes);
        Task<decimal> GetTotalDepositPaidAsync(int tenancyId);
        Task<decimal> GetTotalRentPaidForPeriodAsync(int tenancyId, int year, int month);
        Task<string> GetDepositStatusAsync(int tenancyId, decimal depositRequired);
        Task<string> GetRentStatusForPeriodAsync(int tenancyId, int year, int month);
        Task<IEnumerable<RentLedgerRowViewModel>> GetRentLedgerForMonthAsync(int year, int month, int? houseId = null, string? statusFilter = null, string? searchTerm = null);
        Task<IEnumerable<DepositLedgerRowViewModel>> GetDepositLedgerForMonthAsync(int? houseId = null, string? statusFilter = null, string? searchTerm = null);
        Task<IEnumerable<TransactionRowViewModel>> GetTransactionsAsync(DateTime? startDate = null, DateTime? endDate = null, string? paymentType = null, int? houseId = null, int? tenantId = null, string? method = null);
        Task<IEnumerable<PaymentDetailViewModel>> GetPaymentsForRentChargeAsync(int rentChargeId);
        Task<decimal> GetTotalRentDueThisMonthAsync();
        Task<IEnumerable<DashboardRentDueItem>> GetRentDueInNext7DaysAsync();
        Task<IEnumerable<DashboardOverdueRentItem>> GetOverdueRentAsync();
        Task<bool> UnrecordPaymentAsync(int paymentId, string paymentType);
        Task<bool> DeleteAllTransactionsAsync(); // For testing purposes
    }

    public class DashboardRentDueItem
    {
        public string TenantName { get; set; } = string.Empty;
        public string HouseAddress { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public DateTime DueDate { get; set; }
        public int TenancyId { get; set; }
    }

    public class DashboardOverdueRentItem
    {
        public string TenantName { get; set; } = string.Empty;
        public string HouseAddress { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public int DaysLate { get; set; }
        public int TenancyId { get; set; }
    }
}
