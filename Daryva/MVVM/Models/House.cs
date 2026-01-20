namespace Daryva.MVVM.Models
{
    public class House
    {
        public int HouseId { get; set; }
        public string AddressLine1 { get; set; } = string.Empty;
        public string? AddressLine2 { get; set; }
        public string City { get; set; } = string.Empty;
        public string Postcode { get; set; } = string.Empty;
        public int TotalRooms { get; set; }
        public DateTime CreatedAt { get; set; }
        
        // Calculated properties (not stored in DB)
        public int ActiveTenantCount { get; set; }
        public decimal TotalMonthlyRent { get; set; }
    }
}
