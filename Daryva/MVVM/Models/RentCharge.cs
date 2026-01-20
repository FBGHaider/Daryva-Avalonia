namespace Daryva.MVVM.Models
{
    public class RentCharge
    {
        public int RentChargeId { get; set; }
        public int TenancyId { get; set; }
        public int PeriodYear { get; set; }
        public int PeriodMonth { get; set; }
        public decimal AmountDue { get; set; }
        public DateTime DueDate { get; set; }
        public string Status { get; set; } = "Unpaid"; // Unpaid, PartPaid, Paid, Late
        public DateTime CreatedAt { get; set; }
        
        // Calculated
        public decimal TotalPaid { get; set; }
        public decimal OutstandingAmount => AmountDue - TotalPaid;
        public string PaymentStatus { get; set; } = string.Empty;
        
        // Navigation
        public Tenancy? Tenancy { get; set; }
    }
}
