namespace Daryva.MVVM.Models
{
    public class NotificationTemplate
    {
        public int TemplateId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Channel { get; set; } = string.Empty; // Email, WhatsApp
        public string Type { get; set; } = string.Empty; // RentDue, RentOverdue, MissingDocuments, General
        public string? SubjectTemplate { get; set; }
        public string BodyTemplate { get; set; } = string.Empty;
        public bool IsDefault { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
