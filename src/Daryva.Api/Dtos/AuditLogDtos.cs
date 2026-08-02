namespace Daryva.Api.Dtos;

public class AuditLogQueryRequest
{
    /// <summary>Platform admins may query any org (or omit for platform-wide); Landlords are always restricted to their own current org regardless of what's passed here.</summary>
    public Guid? OrganizationId { get; set; }

    public string? EventType { get; set; }
    public Guid? ActorUserId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public class AuditLogResponse
{
    public Guid Id { get; set; }
    public Guid? OrganizationId { get; set; }
    public Guid ActorUserId { get; set; }
    public string ActorRole { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string? TargetType { get; set; }
    public string? TargetId { get; set; }

    /// <summary>Raw JSON, event-specific shape -- left for the caller to parse.</summary>
    public string? Metadata { get; set; }

    public Guid? SupportSessionId { get; set; }
    public string? IpAddress { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AuditLogListResponse
{
    public List<AuditLogResponse> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
