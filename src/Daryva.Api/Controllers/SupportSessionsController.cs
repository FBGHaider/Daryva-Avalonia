using Daryva.Api.Dtos;
using Daryva.Api.Security;
using Daryva.Api.Security.Interfaces;
using Daryva.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Daryva.Api.Controllers;

/// <summary>
/// Platform-admin Support Mode: time-boxed, logged elevation into a Landlord's org.
/// Authorized purely on ITenantContext.IsPlatformAdmin (see PermissionAuthorizationHandler) --
/// unlike org data endpoints, these are not scoped to a CurrentOrgId the caller is a member of.
/// </summary>
[ApiController]
[Route("api/support-sessions")]
[Authorize]
public class SupportSessionsController : ControllerBase
{
    private readonly ISupportSessionService _supportSessionService;
    private readonly ITenantContext _tenantContext;

    public SupportSessionsController(ISupportSessionService supportSessionService, ITenantContext tenantContext)
    {
        _supportSessionService = supportSessionService;
        _tenantContext = tenantContext;
    }

    [HttpPost]
    [Authorize(Policy = Permissions.SupportSessions.Start)]
    [ProducesResponseType(typeof(SupportSessionResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SupportSessionResponse>> Start([FromBody] StartSupportSessionRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _supportSessionService.StartAsync(_tenantContext.UserId, request, HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
            return CreatedAtAction(nameof(List), new { organizationId = result.OrganizationId }, result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    [HttpPost("{sessionId}/end")]
    [Authorize(Policy = Permissions.SupportSessions.End)]
    [ProducesResponseType(typeof(SupportSessionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SupportSessionResponse>> End(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var result = await _supportSessionService.EndAsync(_tenantContext.UserId, sessionId, HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
        if (result == null)
            return NotFound(new { error = "Support session not found." });

        return Ok(result);
    }

    [HttpGet]
    [Authorize(Policy = Permissions.SupportSessions.View)]
    [ProducesResponseType(typeof(IEnumerable<SupportSessionResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<SupportSessionResponse>>> List([FromQuery] Guid? organizationId, [FromQuery] bool includeEnded = false, CancellationToken cancellationToken = default)
    {
        var result = await _supportSessionService.ListAsync(organizationId, includeEnded, cancellationToken);
        return Ok(result);
    }
}
