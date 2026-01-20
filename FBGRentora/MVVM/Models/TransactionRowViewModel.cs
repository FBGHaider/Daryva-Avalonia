namespace FBGRentora.MVVM.Models
{
    public class TransactionRowViewModel
    {
        public int PaymentId { get; set; }
        public string PaymentType { get; set; } = string.Empty; // "Rent" or "Deposit"
        public DateTime PaidOn { get; set; }
        public string TenantName { get; set; } = string.Empty;
        public string HouseAddress { get; set; } = string.Empty;
        public string PeriodLabel { get; set; } = string.Empty; // "Jan 2026" or empty for deposit
        public decimal Amount { get; set; }
        public string Method { get; set; } = string.Empty;
        public string? Reference { get; set; }
        public string? Notes { get; set; }
        public int? TenancyId { get; set; }
        public int? RentChargeId { get; set; }
    }
}
