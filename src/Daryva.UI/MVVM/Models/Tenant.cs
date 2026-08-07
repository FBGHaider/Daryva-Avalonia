namespace Daryva.MVVM.Models
{
    public class Tenant
    {
        public int TenantId { get; set; }
        public Guid? ApiId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? UniversityName { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsArchived { get; set; }
        
        // Calculated properties
        public string? CurrentHouseAddress { get; set; }
        public int? CurrentHouseId { get; set; }
        public int? CurrentTenancyId { get; set; }
        /// <summary>Move-out date from the tenant's ended tenancy (for archived tenants).</summary>
        public DateTime? LeaveDate { get; set; }

        // Tenant portal status. Named with a "Portal" prefix to avoid confusion with
        // ApiId (this tenant's own API id) -- PortalAppUserId is the linked login's id.
        public Guid? PortalAppUserId { get; set; }
        public DateTime? PortalInviteSentAt { get; set; }
        public DateTime? PortalInviteAcceptedAt { get; set; }

        /// <summary>Derived display status for the Tenants list's Portal column.</summary>
        public string PortalStatus =>
            PortalAppUserId.HasValue ? "Verified" :
            PortalInviteSentAt.HasValue ? "Invited" :
            "Not Invited";
    }
}
