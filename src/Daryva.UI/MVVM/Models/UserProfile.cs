namespace Daryva.MVVM.Models
{
    /// <summary>
    /// User profile for Account page (name, email, phone, time zone, avatar, default org).
    /// </summary>
    public class UserProfile
    {
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string TimeZoneId { get; set; } = "GMT Standard Time";
        public string? AvatarPath { get; set; }
        public Guid? DefaultOrgId { get; set; }
    }
}
