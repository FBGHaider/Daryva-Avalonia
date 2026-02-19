namespace Daryva.MVVM.Models
{
    public class DepositPayment
    {
        public int DepositPaymentId { get; set; }
        public int TenancyId { get; set; }
        public DateTime PaidOn { get; set; }
        public decimal AmountPaid { get; set; }
        public string Method { get; set; } = "BankTransfer"; // BankTransfer, Cash, Card, Other
        public string? Reference { get; set; }
        public string? Notes { get; set; }
        public string? CollectedBy { get; set; }
        
        // Navigation
        public Tenancy? Tenancy { get; set; }
    }
}
