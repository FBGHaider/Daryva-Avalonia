using Daryva.Api.Dtos;

namespace Daryva.Api.Services.Interfaces;

/// <summary>
/// Read-side of the audit log, separate from IAuditLogger (write-side). Owns the visibility
/// rule: platform admins may view any org's trail (or platform-wide with no org filter),
/// Landlords are always restricted to their own current org.
/// </summary>
public interface IAuditLogQueryService
{
    Task<AuditLogListResponse> QueryAsync(AuditLogQueryRequest request, CancellationToken cancellationToken = default);
}
