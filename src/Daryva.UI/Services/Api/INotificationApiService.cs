namespace Daryva.Services.Api;

public class NotificationRecipientDto
{
    public Guid TenantId { get; set; }
    public string TenantName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public Guid? TenancyId { get; set; }
    public string HouseAddressLine1 { get; set; } = string.Empty;
    public string HouseCity { get; set; } = string.Empty;
    public bool HasEmail { get; set; }
    public bool HasWhatsApp { get; set; }
    public decimal? AmountDue { get; set; }
    public DateTime? DueDate { get; set; }
}

public class NotificationTemplateDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? SubjectTemplate { get; set; }
    public string BodyTemplate { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class NotificationResponseDto
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid? TenancyId { get; set; }
    public string Channel { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string ToAddress { get; set; } = string.Empty;
    public string? Subject { get; set; }
    public string Body { get; set; } = string.Empty;
    public DateTime ScheduledFor { get; set; }
    public DateTime? SentAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ProviderMessageId { get; set; }
    public string? Error { get; set; }
    public Guid? TemplateId { get; set; }
    public int AttemptCount { get; set; }
    public string TenantName { get; set; } = string.Empty;
    public string HouseAddressLine1 { get; set; } = string.Empty;
    public string HouseCity { get; set; } = string.Empty;
}

public class CreateNotificationRequestDto
{
    public Guid TenantId { get; set; }
    public Guid? TenancyId { get; set; }
    public string Channel { get; set; } = "Email";
    public string Type { get; set; } = "General";
    public string? Subject { get; set; }
    public string Body { get; set; } = string.Empty;
    public DateTime ScheduledFor { get; set; }
    public Guid? TemplateId { get; set; }
}

public class SendNotificationWithContentRequestDto
{
    public string ToAddress { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
}

public class NotificationFilterDto
{
    public string? Status { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? Channel { get; set; }
    public string? Type { get; set; }
    public Guid? TenantId { get; set; }
    public Guid? HouseId { get; set; }
}

public class RecipientFilterDto
{
    public string TargetType { get; set; } = "Single";
    public Guid? TenantId { get; set; }
    public Guid? HouseId { get; set; }
    public string? StatusFilter { get; set; }
    public int? Month { get; set; }
    public int? Year { get; set; }
}

public interface INotificationApiService
{
    Task<IEnumerable<NotificationRecipientDto>> GetRecipientsAsync(RecipientFilterDto filter);
    Task<IEnumerable<NotificationTemplateDto>> GetTemplatesAsync(string? channel = null, string? type = null);
    Task<NotificationTemplateDto?> GetTemplateByIdAsync(Guid templateId);
    Task<IEnumerable<NotificationResponseDto>> GetNotificationsAsync(NotificationFilterDto filter);
    Task<NotificationResponseDto?> GetNotificationByIdAsync(Guid notificationId);
    Task<NotificationResponseDto> CreateNotificationAsync(CreateNotificationRequestDto request);
    Task<bool> SendNotificationAsync(Guid notificationId);
    Task<bool> SendNotificationWithContentAsync(Guid notificationId, SendNotificationWithContentRequestDto request);
    Task<bool> SendBatchAsync(IEnumerable<Guid> notificationIds);
    Task<int> ProcessDueQueueAsync();
    Task<bool> CancelNotificationAsync(Guid notificationId);
}
