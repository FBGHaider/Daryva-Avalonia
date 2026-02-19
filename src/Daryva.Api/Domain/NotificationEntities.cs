namespace Daryva.Api.Domain;

/// <summary>
/// Notification template (per organization).
/// </summary>
public class NotificationTemplate : IOrgScopedEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty; // Email, SMS, WhatsApp
    public string Type { get; set; } = string.Empty; // RentDue, RentOverdue, MissingDocuments, General
    public string? SubjectTemplate { get; set; }
    public string BodyTemplate { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Organization Organization { get; set; } = null!;
}

/// <summary>
/// Notification queued/sent to a tenant.
/// </summary>
public class Notification : IOrgScopedEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid TenantId { get; set; }
    public Guid? TenancyId { get; set; }
    public string Channel { get; set; } = string.Empty; // Email, SMS, WhatsApp
    public string Type { get; set; } = string.Empty; // RentDue, RentOverdue, MissingDocuments, General
    public string ToAddress { get; set; } = string.Empty;
    public string? Subject { get; set; }
    public string Body { get; set; } = string.Empty;
    public DateTime ScheduledFor { get; set; }
    public DateTime? SentAt { get; set; }
    public string Status { get; set; } = "Pending"; // Pending, Sent, Failed, Cancelled
    public string? ProviderMessageId { get; set; }
    public string? Error { get; set; }
    public Guid? TemplateId { get; set; }

    // Navigation
    public Organization Organization { get; set; } = null!;
    public Tenant Tenant { get; set; } = null!;
    public Tenancy? Tenancy { get; set; }
    public NotificationTemplate? Template { get; set; }
    public ICollection<NotificationAttempt> Attempts { get; set; } = new List<NotificationAttempt>();
}

/// <summary>
/// Attempt to send a notification.
/// </summary>
public class NotificationAttempt : IOrgScopedEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid NotificationId { get; set; }
    public DateTime AttemptedAt { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = string.Empty; // Success, Failed
    public string? Error { get; set; }
    public string? ProviderMessageId { get; set; }

    // Navigation
    public Organization Organization { get; set; } = null!;
    public Notification Notification { get; set; } = null!;
}
