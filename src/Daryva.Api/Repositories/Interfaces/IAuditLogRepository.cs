using Daryva.Api.Domain;

namespace Daryva.Api.Repositories.Interfaces;

public interface IAuditLogRepository
{
    void Add(AuditLog entry);

    /// <summary>Ordered newest-first. organizationId=null means "no org filter" -- callers (the audit query service) decide whether that's allowed for the caller.</summary>
    Task<(List<AuditLog> Items, int TotalCount)> QueryAsync(
        Guid? organizationId,
        string? eventType,
        Guid? actorUserId,
        DateTime? fromDate,
        DateTime? toDate,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}
