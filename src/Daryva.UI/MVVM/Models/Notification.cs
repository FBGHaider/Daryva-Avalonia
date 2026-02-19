namespace Daryva.MVVM.Models
{
    public class Notification
    {
        public int NotificationId { get; set; }
        public Guid? ApiId { get; set; }
        public int TenantId { get; set; }
        public int? TenancyId { get; set; }
        public string Channel { get; set; } = string.Empty; // Email, WhatsApp
        public string Type { get; set; } = string.Empty; // RentDue, RentLate, DocumentExpiring, General
        public string ToAddress { get; set; } = string.Empty;
        public string? Subject { get; set; }
        public string Body { get; set; } = string.Empty;
        public DateTime ScheduledFor { get; set; }
        public DateTime? SentAt { get; set; }
        public string Status { get; set; } = "Pending"; // Pending, Sent, Failed, Cancelled
        public string? ProviderMessageId { get; set; }
        public string? Error { get; set; }
        public int? TemplateId { get; set; }
        
        // Navigation
        public Tenant? Tenant { get; set; }
        public Tenancy? Tenancy { get; set; }
        public NotificationTemplate? Template { get; set; }
        public List<NotificationAttempt> Attempts { get; set; } = new();
    }
}
