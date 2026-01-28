namespace Daryva.MVVM.ViewModels
{
    public class NotificationRowViewModel
    {
        public int NotificationId { get; set; }
        public DateTime ScheduledFor { get; set; }
        public DateTime? SentAt { get; set; }
        public string TenantName { get; set; } = string.Empty;
        public string HouseAddress { get; set; } = string.Empty;
        public string Channel { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string BodyPreview { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int AttemptCount { get; set; }
        public string? Error { get; set; }

        public string SentDisplay { get; set; } = "-";
        public string ScheduledDisplay { get; set; } = string.Empty;
    }
}
