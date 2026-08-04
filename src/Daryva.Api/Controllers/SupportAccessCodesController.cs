using Daryva.Api.Dtos;
using Daryva.Api.Security;
using Daryva.Api.Security.Interfaces;
using Daryva.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Daryva.Api.Controllers;

/// <summary>
/// Short-lived codes a Landlord generates so a platform admin can look up their exact org for
/// Support Mode without searching by email -- e.g. read out over a phone call. Resolving a code
/// only identifies the org; starting an actual Support Session is a separate, audited step via
/// SupportSessionsController.
/// </summary>
[ApiController]
[Route("api/support-access-codes")]
[Authorize]
public class SupportAccessCodesController : ControllerBase
{
    private readonly ISupportAccessCodeService _service;
    private readonly ITenantContext _tenantContext;

    public SupportAccessCodesController(ISupportAccessCodeService service, ITenantContext tenantContext)
    {
        _service = service;
        _tenantContext = tenantContext;
    }

    /// <summary>
    /// Generate a code for the caller's current organization. Any real member (Landlord) can do
    /// this for their own org -- it identifies the org to an admin, it does not itself grant access.
    ///
    /// POST /api/support-access-codes
    /// Headers: X-Org-Id (required)
    /// </summary>
    [HttpPost]
    [Authorize(Policy = Permissions.Organization.ManageMembers)]
    public async Task<ActionResult<SupportAccessCodeResponse>> Generate(CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.CurrentOrgId.HasValue)
            return BadRequest(new { error = "Organization context not set." });

        try
        {
            var response = await _service.GenerateAsync(_tenantContext.UserId, _tenantContext.CurrentOrgId.Value, cancellationToken);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Resolve a code to the org it identifies -- platform admin only. Single-use: a code can only
    /// be resolved once, and only within its expiry window.
    ///
    /// POST /api/support-access-codes/resolve
    /// </summary>
    [HttpPost("resolve")]
    [Authorize(Policy = Permissions.SupportSessions.Start)]
    public async Task<ActionResult<ResolvedSupportAccessCodeResponse>> Resolve(
        [FromBody] ResolveSupportAccessCodeRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await _service.ResolveAsync(_tenantContext.UserId, request.Code, cancellationToken);
        if (result == null)
            return NotFound(new { error = "That code is invalid, expired, or has already been used." });

        return Ok(result);
    }
}
