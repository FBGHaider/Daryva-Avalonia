namespace Daryva.Api.Dtos;

public class NotificationRecipientResponse
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

public class NotificationTemplateResponse
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

public class NotificationResponse
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

public class CreateNotificationRequest
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

public class SendNotificationWithContentRequest
{
    public string ToAddress { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
}

public class NotificationFilterRequest
{
    public string? Status { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? Channel { get; set; }
    public string? Type { get; set; }
    public Guid? TenantId { get; set; }
    public Guid? HouseId { get; set; }
}

public class RecipientFilterRequest
{
    public string TargetType { get; set; } = "Single"; // Single, House, All
    public Guid? TenantId { get; set; }
    public Guid? HouseId { get; set; }
    public string? StatusFilter { get; set; } // Due, Overdue, All
    public int? Month { get; set; }
    public int? Year { get; set; }
}

public class NotificationTemplateRequest
{
    public string Name { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? SubjectTemplate { get; set; }
    public string BodyTemplate { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
}

public class NotificationAttemptResponse
{
    public Guid Id { get; set; }
    public Guid NotificationId { get; set; }
    public DateTime AttemptedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Error { get; set; }
    public string? ProviderMessageId { get; set; }
}
