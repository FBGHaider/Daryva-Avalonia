using Daryva.Api.Dtos;
using Daryva.Api.Repositories.Interfaces;
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
    private readonly ITenantRepository _tenantRepository;
    private readonly ILogger<MeController> _logger;

    public MeController(IMeService meService, ITenantContext tenantContext, ITenantRepository tenantRepository, ILogger<MeController> logger)
    {
        _meService = meService;
        _tenantContext = tenantContext;
        _tenantRepository = tenantRepository;
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
    /// Whether this login has a Tenant identity at all, and which org it's in. Deliberately does
    /// NOT read _tenantContext.CurrentRole/CurrentOrgId -- those require TenantContextMiddleware
    /// to have already auto-selected a single unambiguous org, which fails (400) for a caller who
    /// belongs to more than one org (e.g. a landlord who was themselves invited as a tenant by a
    /// different landlord). This route is listed in TenantContextMiddleware.IsPublicRoute for
    /// exactly that reason -- it skips org resolution and looks up Tenant links directly instead,
    /// the same way GetAllByAppUserIdAsync is already used pre-org-resolution in TenantContext
    /// and TenantContextMiddleware itself.
    /// </summary>
    [HttpGet("tenant-access")]
    [ProducesResponseType(typeof(TenantAccessResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TenantAccessResponseDto>> GetTenantAccess(CancellationToken cancellationToken = default)
    {
        var userId = _tenantContext.UserId;
        if (string.IsNullOrEmpty(userId) || userId == "unknown-user" || !Guid.TryParse(userId, out var userGuid))
            return Unauthorized(new { error = "Not authenticated." });

        var linkedTenants = await _tenantRepository.GetAllByAppUserIdAsync(userGuid, cancellationToken);
        // A person could in principle be invited as a tenant by more than one landlord org (see
        // GetAllByAppUserIdAsync's doc comment); picking the first is a known limitation, not a
        // decision that a second such tenancy is unsupported -- just not surfaced by this check.
        var tenant = linkedTenants.FirstOrDefault();

        return Ok(new TenantAccessResponseDto
        {
            IsTenant = tenant != null,
            TenantOrgId = tenant?.OrganizationId
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
