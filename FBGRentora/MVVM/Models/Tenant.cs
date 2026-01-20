namespace FBGRentora.MVVM.Models
{
    public class Tenant
    {
        public int TenantId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? UniversityName { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsArchived { get; set; }
        
        // Calculated properties
        public string? CurrentHouseAddress { get; set; }
        public int? CurrentTenancyId { get; set; }
    }
}
