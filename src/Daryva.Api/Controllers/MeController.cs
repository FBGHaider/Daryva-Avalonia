using Daryva.Api.Dtos;
using Daryva.Api.Security;
using Daryva.Api.Security.Interfaces;
using Daryva.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Daryva.Api.Controllers;

/// <summary>
/// Current user profile, organisations, and onboarding state.
/// Used by app.daryva.com to drive redirects (org setup, profile setup).
/// </summary>
[ApiController]
[Route("api/me")]
[Authorize]
public class MeController : ControllerBase
{
    private readonly IMeService _meService;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<MeController> _logger;

    public MeController(IMeService meService, ITenantContext tenantContext, ILogger<MeController> logger)
    {
        _meService = meService;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    /// <summary>
    /// Get current user profile, organisations, and onboarding flags.
    /// On first call, ensures AppUserProfile exists (create from JWT sub/email).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(MeResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<MeResponseDto>> GetMe(CancellationToken cancellationToken = default)
    {
        var userId = _tenantContext.UserId;
        if (string.IsNullOrEmpty(userId) || userId == "unknown-user")
            return Unauthorized(new { error = "Not authenticated." });

        var email = ClaimsHelper.GetEmailFromClaims(User);
        await _meService.EnsureUserProfileAsync(userId, email, cancellationToken);

        var me = await _meService.GetMeAsync(userId, cancellationToken);
        if (me == null)
            return Unauthorized(new { error = "User profile not found." });

        return Ok(me);
    }

    /// <summary>
    /// Whether the caller's resolved role in their current org is Tenant. The tenant portal
    /// calls this immediately after login/accept-invite -- unlike /api/me, this path is not a
    /// public route, so TenantContextMiddleware has already run org auto-select and
    /// ResolveCurrentRoleAsync before this action executes.
    /// </summary>
    [HttpGet("tenant-access")]
    [ProducesResponseType(typeof(TenantAccessResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult<TenantAccessResponseDto> GetTenantAccess()
    {
        var userId = _tenantContext.UserId;
        if (string.IsNullOrEmpty(userId) || userId == "unknown-user")
            return Unauthorized(new { error = "Not authenticated." });

        return Ok(new TenantAccessResponseDto
        {
            IsTenant = _tenantContext.CurrentRole == Roles.Tenant
        });
    }

    /// <summary>
    /// Update current user profile (DisplayName, Phone, TimeZoneId).
    /// Only provided fields are updated; validation applied per field.
    /// </summary>
    [HttpPut]
    [ProducesResponseType(typeof(MeUserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<MeUserDto>> UpdateMe([FromBody] UpdateMeRequest request, CancellationToken cancellationToken = default)
    {
        var userId = _tenantContext.UserId;
        if (string.IsNullOrEmpty(userId) || userId == "unknown-user")
            return Unauthorized(new { error = "Not authenticated." });

        if (request == null)
            return BadRequest(new { error = "Request body is required." });

        try
        {
            var updated = await _meService.UpdateProfileAsync(userId, request, cancellationToken);
            if (updated == null)
                return Unauthorized(new { error = "User profile not found." });
            return Ok(updated);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
