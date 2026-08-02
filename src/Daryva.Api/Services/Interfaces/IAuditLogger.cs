namespace Daryva.Api.Services.Interfaces;

/// <summary>
/// Stages an AuditLog entry via the repository -- does not call SaveChanges itself.
/// Callers already persist their own changes through IUnitOfWork; staging here lets
/// the audit entry commit atomically with whatever action it's recording.
/// </summary>
public interface IAuditLogger
{
    void Log(
        Guid actorUserId,
        string actorRole,
        string eventType,
        Guid? organizationId = null,
        string? targetType = null,
        string? targetId = null,
        object? metadata = null,
        Guid? supportSessionId = null,
        string? ipAddress = null);
}
