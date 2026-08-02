using System.Text.Json;
using Daryva.Api.Domain;
using Daryva.Api.Repositories.Interfaces;
using Daryva.Api.Services.Interfaces;

namespace Daryva.Api.Services;

public class AuditLogger : IAuditLogger
{
    private readonly IAuditLogRepository _auditLogRepository;

    public AuditLogger(IAuditLogRepository auditLogRepository)
    {
        _auditLogRepository = auditLogRepository;
    }

    public void Log(
        Guid actorUserId,
        string actorRole,
        string eventType,
        Guid? organizationId = null,
        string? targetType = null,
        string? targetId = null,
        object? metadata = null,
        Guid? supportSessionId = null,
        string? ipAddress = null)
    {
        var entry = new AuditLog
        {
            OrganizationId = organizationId,
            ActorUserId = actorUserId,
            ActorRole = actorRole,
            EventType = eventType,
            TargetType = targetType,
            TargetId = targetId,
            Metadata = metadata == null ? null : JsonSerializer.Serialize(metadata),
            SupportSessionId = supportSessionId,
            IpAddress = ipAddress
        };

        _auditLogRepository.Add(entry);
    }
}
