namespace LandLordBuddy.MVVM.Models
{
    public class Document
    {
        public int DocumentId { get; set; }
        public int? TenantId { get; set; }
        public int? TenancyId { get; set; }
        public int? HouseId { get; set; }
        public string Type { get; set; } = string.Empty; // StudentConfirmationLetter, PhotoId, RightToRent, TenancyAgreementSigned, GuarantorAgreement, InventoryCheckIn, DepositProtectionCertificate, NoticeToLeave, Other
        public string DisplayName { get; set; } = string.Empty; // Human-readable name (e.g., "Student Letter 2025/26")
        public string FileName { get; set; } = string.Empty;
        public string? FileMimeType { get; set; }
        public string? StoragePath { get; set; }
        public string? Source { get; set; } // Uploaded or Generated
        public DateTime UploadedAt { get; set; }
        public DateTime? ValidFrom { get; set; }
        public DateTime? ValidTo { get; set; }
        public int Version { get; set; } = 1;
        public bool IsActive { get; set; } = true;
        
        // Navigation
        public Tenant? Tenant { get; set; }
        public Tenancy? Tenancy { get; set; }
        public House? House { get; set; }
        
        // Calculated/Temporary property for display
        public string TenantName
        {
            get
            {
                if (Tenant != null)
                    return Tenant.FullName;
                if (Tenancy?.Tenant != null)
                    return Tenancy.Tenant.FullName;
                return string.Empty;
            }
        }
        
        // Calculated
        public bool IsExpiringSoon => ValidTo.HasValue && ValidTo.Value <= DateTime.Now.AddDays(30);
        public bool IsExpired => ValidTo.HasValue && ValidTo.Value < DateTime.Now;
    }
}
