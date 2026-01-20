namespace FBGRentora.MVVM.Models
{
    public class Tenancy
    {
        public int TenancyId { get; set; }
        public int HouseId { get; set; }
        public int TenantId { get; set; }
        public DateTime MoveInDate { get; set; }
        public DateTime? MoveOutDate { get; set; }
        public decimal RentAmountMonthly { get; set; }
        public decimal DepositAmount { get; set; }
        public byte PaymentDueDay { get; set; }
        public string Status { get; set; } = "Active"; // Active, Ended
        public string? Notes { get; set; }
        
        // Navigation properties (loaded separately)
        public House? House { get; set; }
        public Tenant? Tenant { get; set; }
    }
}
