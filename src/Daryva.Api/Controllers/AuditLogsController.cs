using Daryva.Api.Dtos;
using Daryva.Api.Security;
using Daryva.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Daryva.Api.Controllers;

/// <summary>
/// Read-only audit trail. Visibility is enforced in IAuditLogQueryService, not here: platform
/// admins see any org (or platform-wide), Landlords are restricted to their own current org.
/// Tenants hold no Audit.View permission, so [Authorize(Policy = ...)] alone denies them.
/// </summary>
[ApiController]
[Route("api/audit-logs")]
[Authorize]
public class AuditLogsController : ControllerBase
{
    private readonly IAuditLogQueryService _auditLogQueryService;

    public AuditLogsController(IAuditLogQueryService auditLogQueryService)
    {
        _auditLogQueryService = auditLogQueryService;
    }

    [HttpGet]
    [Authorize(Policy = Permissions.Audit.View)]
    [ProducesResponseType(typeof(AuditLogListResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<AuditLogListResponse>> Query([FromQuery] AuditLogQueryRequest request, CancellationToken cancellationToken = default)
    {
        var result = await _auditLogQueryService.QueryAsync(request, cancellationToken);
        return Ok(result);
    }
}
