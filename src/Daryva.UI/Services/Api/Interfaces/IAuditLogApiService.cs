using System.Text.Json.Serialization;

namespace Daryva.Services.Api;

public class AuditLogEntryDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("organizationId")]
    public Guid? OrganizationId { get; set; }

    [JsonPropertyName("actorUserId")]
    public Guid ActorUserId { get; set; }

    [JsonPropertyName("actorRole")]
    public string ActorRole { get; set; } = string.Empty;

    [JsonPropertyName("eventType")]
    public string EventType { get; set; } = string.Empty;

    [JsonPropertyName("targetType")]
    public string? TargetType { get; set; }

    [JsonPropertyName("targetId")]
    public string? TargetId { get; set; }

    /// <summary>Raw JSON, event-specific shape.</summary>
    [JsonPropertyName("metadata")]
    public string? Metadata { get; set; }

    [JsonPropertyName("supportSessionId")]
    public Guid? SupportSessionId { get; set; }

    [JsonPropertyName("ipAddress")]
    public string? IpAddress { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }
}

public class AuditLogListResultDto
{
    [JsonPropertyName("items")]
    public List<AuditLogEntryDto> Items { get; set; } = new();

    [JsonPropertyName("totalCount")]
    public int TotalCount { get; set; }

    [JsonPropertyName("page")]
    public int Page { get; set; }

    [JsonPropertyName("pageSize")]
    public int PageSize { get; set; }
}

public class AuditLogQuery
{
    public string? EventType { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public interface IAuditLogApiService
{
    Task<AuditLogListResultDto> QueryAsync(AuditLogQuery query, CancellationToken cancellationToken = default);
}
