using FBGRentora.MVVM.Models;

namespace FBGRentora.Services.Business
{
    public interface INotificationService
    {
        Task<IEnumerable<NotificationRecipient>> BuildRecipientsAsync(RecipientFilter filter);
        Task<string> RenderTemplateAsync(string templateBody, string? subjectTemplate, NotificationContext context);
        Task<Notification> QueueNotificationAsync(NotificationDto dto);
        Task<bool> SendNotificationAsync(int notificationId);
        Task<bool> SendBatchAsync(IEnumerable<int> notificationIds);
        Task<IEnumerable<Notification>> GetNotificationsAsync(NotificationFilter filter);
        Task<Notification?> GetNotificationByIdAsync(int notificationId);
        Task CancelNotificationAsync(int notificationId);
        Task<IEnumerable<NotificationTemplate>> GetTemplatesAsync(string? channel = null, string? type = null);
        Task<NotificationTemplate?> GetTemplateByIdAsync(int templateId);
    }

    public class RecipientFilter
    {
        public string TargetType { get; set; } = "Single"; // Single, House, AllWithStatus
        public int? TenantId { get; set; }
        public int? HouseId { get; set; }
        public string? StatusFilter { get; set; } // Due, Overdue, MissingDocs
        public int? Month { get; set; }
        public int? Year { get; set; }
    }

    public class NotificationRecipient
    {
        public int TenantId { get; set; }
        public string TenantName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public int? TenancyId { get; set; }
        public string HouseAddress { get; set; } = string.Empty;
        public bool HasEmail { get; set; }
        public bool HasWhatsApp { get; set; }
        public decimal? AmountDue { get; set; }
        public DateTime? DueDate { get; set; }
    }

    public class NotificationContext
    {
        public string TenantName { get; set; } = string.Empty;
        public string HouseAddress { get; set; } = string.Empty;
        public decimal? AmountDue { get; set; }
        public DateTime? DueDate { get; set; }
        public string Month { get; set; } = string.Empty;
        public decimal? DepositRemaining { get; set; }
        public string PayInstructions { get; set; } = "Please contact your landlord for payment instructions.";
        public string? Message { get; set; }
    }

    public class NotificationDto
    {
        public int TenantId { get; set; }
        public int? TenancyId { get; set; }
        public string Channel { get; set; } = "Email";
        public string Type { get; set; } = "General";
        public string Subject { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public DateTime ScheduledFor { get; set; }
        public int? TemplateId { get; set; }
    }

    public class NotificationFilter
    {
        public string? Status { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string? Channel { get; set; }
        public string? Type { get; set; }
        public int? TenantId { get; set; }
        public int? HouseId { get; set; }
    }
}
