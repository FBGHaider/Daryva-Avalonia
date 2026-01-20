namespace Daryva.MVVM.Models
{
    public class RentPayment
    {
        public int RentPaymentId { get; set; }
        public int TenancyId { get; set; }
        public int? RentChargeId { get; set; }
        public DateTime PaidOn { get; set; }
        public decimal AmountPaid { get; set; }
        public string Method { get; set; } = "BankTransfer"; // BankTransfer, Cash, Card, Other
        public string? Reference { get; set; }
        public string? Notes { get; set; }
        
        // Navigation
        public Tenancy? Tenancy { get; set; }
        public RentCharge? RentCharge { get; set; }
    }
}
