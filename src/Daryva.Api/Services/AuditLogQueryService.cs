using Daryva.Api.Domain;
using Daryva.Api.Dtos;
using Daryva.Api.Repositories.Interfaces;
using Daryva.Api.Security.Interfaces;
using Daryva.Api.Services.Interfaces;

namespace Daryva.Api.Services;

public class AuditLogQueryService : IAuditLogQueryService
{
    private const int DefaultPageSize = 50;
    private const int MaxPageSize = 200;

    private readonly IAuditLogRepository _auditLogRepository;
    private readonly ITenantContext _tenantContext;

    public AuditLogQueryService(IAuditLogRepository auditLogRepository, ITenantContext tenantContext)
    {
        _auditLogRepository = auditLogRepository;
        _tenantContext = tenantContext;
    }

    public async Task<AuditLogListResponse> QueryAsync(AuditLogQueryRequest request, CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize <= 0 ? DefaultPageSize : request.PageSize, 1, MaxPageSize);

        // Platform admins may view any org's trail (or platform-wide with OrganizationId=null).
        // Everyone else is hard-restricted to their own current org -- whatever OrganizationId
        // they passed is ignored, never trusted, so a Landlord can't probe another org's trail
        // just by guessing its id.
        Guid? organizationId;
        if (_tenantContext.IsPlatformAdmin)
        {
            organizationId = request.OrganizationId;
        }
        else if (_tenantContext.CurrentOrgId.HasValue)
        {
            organizationId = _tenantContext.CurrentOrgId.Value;
        }
        else
        {
            return new AuditLogListResponse { Page = page, PageSize = pageSize };
        }

        var (items, totalCount) = await _auditLogRepository.QueryAsync(
            organizationId,
            request.EventType,
            request.ActorUserId,
            request.FromDate,
            request.ToDate,
            page,
            pageSize,
            cancellationToken);

        return new AuditLogListResponse
        {
            Items = items.Select(ToResponse).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    private static AuditLogResponse ToResponse(AuditLog log)
    {
        return new AuditLogResponse
        {
            Id = log.Id,
            OrganizationId = log.OrganizationId,
            ActorUserId = log.ActorUserId,
            ActorRole = log.ActorRole,
            EventType = log.EventType,
            TargetType = log.TargetType,
            TargetId = log.TargetId,
            Metadata = log.Metadata,
            SupportSessionId = log.SupportSessionId,
            IpAddress = log.IpAddress,
            CreatedAt = log.CreatedAt
        };
    }
}
