namespace Daryva.MVVM.Models
{
    /// <summary>
    /// Type of notification for the header intelligence feed.
    /// </summary>
    public enum NotificationFeedType
    {
        OverdueRent,
        RentDueSoon,
        DocsExpiring,
        PaymentReceived,
        TeamActivity,
        System,
        PortalSignupCompleted
    }

    /// <summary>
    /// Severity for styling (icon/badge).
    /// </summary>
    public enum NotificationSeverity
    {
        Info,
        Warning,
        Critical
    }

    /// <summary>
    /// Target type for notification navigation.
    /// </summary>
    public enum NotificationTargetType
    {
        Tenant,
        House,
        Document,
        Payment,
        Tenancy
    }

    /// <summary>
    /// Optional navigation target for a notification.
    /// </summary>
    public record NotificationNavigationTarget(NotificationTargetType TargetType, Guid? TargetId);

    /// <summary>
    /// Model for a single item in the notification feed (header bell drawer).
    /// </summary>
    public class NotificationItem
    {
        public Guid Id { get; set; }
        public NotificationFeedType Type { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DateTimeOffset CreatedAt { get; set; }
        public bool IsRead { get; set; }
        public NotificationSeverity Severity { get; set; } = NotificationSeverity.Info;
        public NotificationNavigationTarget? NavigationTarget { get; set; }
    }
}
