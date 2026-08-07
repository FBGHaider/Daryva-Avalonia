namespace Daryva.MVVM.Models
{
    public enum UpcomingEventCategory
    {
        RentDueSoon,
        DocumentExpiring
    }

    /// <summary>One row in the dashboard's "Upcoming" widget. Only two categories exist because
    /// only two are backed by real data in this product today -- there is no maintenance-ticket or
    /// lease-renewal concept anywhere in Daryva, so neither is represented here.</summary>
    public class UpcomingEventItem
    {
        public UpcomingEventCategory Category { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public string DateDisplay { get; set; } = string.Empty;

        /// <summary>True when within the "needs attention soon" window -- drives Warning vs Info
        /// severity in the UI (see NotificationFeedService's equivalent thresholds).</summary>
        public bool IsUrgent { get; set; }
    }
}
