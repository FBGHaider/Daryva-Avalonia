namespace Daryva.MVVM.Models
{
    /// <summary>
    /// Personal notification preferences for the Account page.
    /// </summary>
    public class UserNotificationPreferences
    {
        public bool RentAlertsEnabled { get; set; } = true;
        public bool DocsExpiryEnabled { get; set; } = true;
        public bool TeamActivityEnabled { get; set; } = true;
        public bool ProductUpdatesEnabled { get; set; } = true;
        public bool InAppEnabled { get; set; } = true;
        public bool EmailEnabled { get; set; } = false;
    }
}
