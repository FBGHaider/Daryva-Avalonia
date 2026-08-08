using Daryva.MVVM.Models;

namespace Daryva.Services.Business
{
    public interface IPaymentService
    {
        Task RecordPaymentAsync(int tenancyId, decimal depositAmount, decimal rentAmount, int rentYear, int rentMonth, DateTime paymentDate, string method, string? reference, string? notes, string? collectedBy = null, bool useDepositForRent = false);
        Task<decimal> GetTotalDepositPaidAsync(int tenancyId);
        Task<decimal> GetTotalRentPaidForPeriodAsync(int tenancyId, int year, int month);
        Task<string> GetDepositStatusAsync(int tenancyId, decimal depositRequired);
        Task<string> GetRentStatusForPeriodAsync(int tenancyId, int year, int month);
        Task<IEnumerable<RentLedgerRowViewModel>> GetRentLedgerForMonthAsync(int year, int month, int? houseId = null, string? statusFilter = null, string? searchTerm = null);
        Task<IEnumerable<DepositLedgerRowViewModel>> GetDepositLedgerForMonthAsync(int year, int month, int? houseId = null, string? statusFilter = null, string? searchTerm = null);
        Task<IEnumerable<TransactionRowViewModel>> GetTransactionsAsync(DateTime? startDate = null, DateTime? endDate = null, string? paymentType = null, int? houseId = null, int? tenantId = null, string? method = null);
        Task<IEnumerable<PaymentDetailViewModel>> GetPaymentsForRentChargeAsync(int rentChargeId);
        /// <summary>Total unpaid balance for a given month, using payments linked to each charge (RentChargeId). One balance per tenancy; dedupes duplicate charges.</summary>
        Task<decimal> GetTotalUnpaidBalanceForMonthAsync(int year, int month);
        Task<IEnumerable<DashboardOverdueRentItem>> GetOverdueRentAsync();
        Task<IEnumerable<DepositReturnReminderItem>> GetDepositReturnRemindersAsync();
        Task<bool> UnrecordPaymentAsync(int paymentId, string paymentType);
        Task<bool> DeleteAllTransactionsAsync(); // For testing purposes
        /// <summary>Repairs rent payments: links each payment to the correct RentCharge for its PaidOn period (creates charge if missing). Returns number of payments updated.</summary>
        Task<int> RepairRentPaymentChargeLinksAsync();
        /// <summary>Record that deposit was paid back to the tenant (so they disappear from deposit-return list and deposit ledger after that date).</summary>
        Task RecordDepositReturnedAsync(int tenancyId, DateTime returnedDate, decimal amountReturned, string? notes = null);
    }

    public class DashboardOverdueRentItem
    {
        public string TenantName { get; set; } = string.Empty;
        public string HouseAddress { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public int DaysLate { get; set; }
        public int TenancyId { get; set; }
    }

    public class DepositReturnReminderItem
    {
        public int TenancyId { get; set; }
        public string TenantName { get; set; } = string.Empty;
        public string HouseAddress { get; set; } = string.Empty;
        public DateTime LeaveDate { get; set; }
        public string LeaveDateDisplay { get; set; } = string.Empty;
        public decimal AmountToReturn { get; set; }
    }
}
