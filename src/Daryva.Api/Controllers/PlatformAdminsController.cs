using Daryva.Api.Dtos;
using Daryva.Api.Security;
using Daryva.Api.Security.Interfaces;
using Daryva.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Daryva.Api.Controllers;

/// <summary>
/// Manage which accounts hold AppUser.IsPlatformAdmin. Granting only happens via server config
/// (Admin:BootstrapEmails, see PlatformAdminBootstrapper) plus a restart -- there is deliberately
/// no grant endpoint here. Revoking is the one action available at runtime.
/// </summary>
[ApiController]
[Route("api/platform-admins")]
[Authorize]
public class PlatformAdminsController : ControllerBase
{
    private readonly IPlatformAdminService _platformAdminService;
    private readonly ITenantContext _tenantContext;

    public PlatformAdminsController(IPlatformAdminService platformAdminService, ITenantContext tenantContext)
    {
        _platformAdminService = platformAdminService;
        _tenantContext = tenantContext;
    }

    [HttpGet]
    [Authorize(Policy = Permissions.Platform.ManageUsers)]
    [ProducesResponseType(typeof(IEnumerable<PlatformAdminResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<PlatformAdminResponse>>> List(CancellationToken cancellationToken = default)
    {
        var result = await _platformAdminService.ListAsync(cancellationToken);
        return Ok(result);
    }

    [HttpPost("{userId}/revoke")]
    [Authorize(Policy = Permissions.Platform.ManageUsers)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Revoke(Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            await _platformAdminService.RevokeAsync(_tenantContext.UserId, userId, HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
            return NoContent();
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
}
