using System.Security.Claims;
using Daryva.Api.Dtos;
using Daryva.Api.Security;
using Daryva.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Daryva.Api.Controllers;

/// <summary>
/// API endpoints for organization management.
/// Supports multi-tenancy: users can belong to multiple organizations.
/// </summary>
[ApiController]
[Route("api/orgs")]
[Authorize]
public class OrgsController : ControllerBase
{
    private readonly IOrganizationService _orgService;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<OrgsController> _logger;

    public OrgsController(
        IOrganizationService orgService,
        ITenantContext tenantContext,
        ILogger<OrgsController> logger)
    {
        _orgService = orgService;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    /// <summary>
    /// Create a new organization.
    /// Current user becomes the Owner.
    /// The organization ID is automatically set in CurrentOrgId context.
    ///
    /// POST /api/orgs
    /// {
    ///   "name": "John's Property Management"
    /// }
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(OrganizationResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<OrganizationResponse>> CreateOrganization(
        [FromBody] CreateOrganizationRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var callerEmail = User.FindFirstValue(ClaimTypes.Email) ?? User.FindFirstValue("email");
            var org = await _orgService.CreateOrganizationAsync(
                _tenantContext.UserId,
                request,
                callerEmail,
                cancellationToken);

            _logger.LogInformation(
                "User {UserId} created organization {OrgId}.",
                _tenantContext.UserId, org.Id);

            return CreatedAtAction(nameof(GetOrganization), new { orgId = org.Id }, org);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get all organizations the user belongs to.
    ///
    /// GET /api/orgs
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<OrganizationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IEnumerable<OrganizationResponse>>> GetOrganizations(
        CancellationToken cancellationToken = default)
    {
        var orgs = await _orgService.GetUserOrganizationsAsync(
            _tenantContext.UserId,
            cancellationToken);

        return Ok(orgs);
    }

    /// <summary>
    /// Get a specific organization by ID (if user is member).
    ///
    /// GET /api/orgs/{orgId}
    /// </summary>
    [HttpGet("{orgId}")]
    [ProducesResponseType(typeof(OrganizationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<OrganizationResponse>> GetOrganization(
        Guid orgId,
        CancellationToken cancellationToken = default)
    {
        var org = await _orgService.GetOrganizationAsync(
            orgId,
            _tenantContext.UserId,
            cancellationToken);

        if (org == null)
            return NotFound(new { error = "Organization not found or not a member." });

        return Ok(org);
    }

    /// <summary>
    /// Update organization (e.g. rename). Owner only.
    /// PATCH /api/orgs/{orgId}
    /// { "name": "New name" }
    /// </summary>
    [HttpPatch("{orgId}")]
    [ProducesResponseType(typeof(OrganizationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<OrganizationResponse>> UpdateOrganization(
        Guid orgId,
        [FromBody] UpdateOrganizationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request == null)
            return BadRequest(new { error = "Request body required." });
        try
        {
            var org = await _orgService.UpdateOrganizationAsync(
                orgId,
                _tenantContext.UserId,
                request,
                cancellationToken);
            if (org == null)
                return NotFound(new { error = "Organization not found or not a member." });
            return Ok(org);
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Add a member to an organization (by email).
    /// User must be a member of the organization (role validation could be added).
    ///
    /// POST /api/orgs/{orgId}/members
    /// {
    ///   "email": "newmember@example.com",
    ///   "role": "Member"
    /// }
    /// </summary>
    [HttpPost("{orgId}/members")]
    [ProducesResponseType(typeof(OrganizationMemberResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<OrganizationMemberResponse>> AddMember(
        Guid orgId,
        [FromBody] AddMemberRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var member = await _orgService.AddMemberAsync(
                orgId,
                _tenantContext.UserId,
                request,
                cancellationToken);

            _logger.LogInformation(
                "User {UserId} added member {Email} to organization {OrgId}.",
                _tenantContext.UserId, request.Email, orgId);

            return CreatedAtAction(nameof(GetMembers), new { orgId }, member);
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get all members of an organization (if user is member).
    ///
    /// GET /api/orgs/{orgId}/members
    /// </summary>
    [HttpGet("{orgId}/members")]
    [ProducesResponseType(typeof(IEnumerable<OrganizationMemberResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IEnumerable<OrganizationMemberResponse>>> GetMembers(
        Guid orgId,
        CancellationToken cancellationToken = default)
    {
        // Check if user is member
        var org = await _orgService.GetOrganizationAsync(
            orgId,
            _tenantContext.UserId,
            cancellationToken);

        if (org == null)
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                error = "You are not a member of this organization."
            });

        var members = await _orgService.GetOrganizationMembersAsync(
            orgId,
            _tenantContext.UserId,
            cancellationToken);

        return Ok(members);
    }

    /// <summary>
    /// Delete an organization (Owner only).
    ///
    /// DELETE /api/orgs/{orgId}
    /// </summary>
    [HttpDelete("{orgId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> DeleteOrganization(
        Guid orgId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var deleted = await _orgService.DeleteOrganizationAsync(
                orgId,
                _tenantContext.UserId,
                cancellationToken);

            if (!deleted)
                return NotFound(new { error = "Organization not found or not a member." });

            _logger.LogInformation(
                "User {UserId} deleted organization {OrgId}.",
                _tenantContext.UserId,
                orgId);

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Create an invite token for an organization (Owner/Admin only).
    ///
    /// POST /api/orgs/{orgId}/invites
    /// </summary>
    [HttpPost("{orgId}/invites")]
    [ProducesResponseType(typeof(CreateOrgInviteResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<CreateOrgInviteResponse>> CreateInvite(
        Guid orgId,
        [FromBody] CreateOrgInviteRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _orgService.CreateInviteAsync(orgId, _tenantContext.UserId, request, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Accept invite token and join organization.
    ///
    /// POST /api/orgs/join/invite
    /// </summary>
    [HttpPost("join/invite")]
    [ProducesResponseType(typeof(JoinOrganizationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<JoinOrganizationResponse>> AcceptInvite(
        [FromBody] AcceptOrgInviteRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _orgService.AcceptInviteAsync(_tenantContext.UserId, request, cancellationToken);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Generate or rotate organization join code (Owner/Admin only).
    ///
    /// POST /api/orgs/{orgId}/join-code
    /// </summary>
    [HttpPost("{orgId}/join-code")]
    [ProducesResponseType(typeof(GenerateOrgJoinCodeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<GenerateOrgJoinCodeResponse>> GenerateJoinCode(
        Guid orgId,
        [FromBody] GenerateOrgJoinCodeRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _orgService.GenerateJoinCodeAsync(orgId, _tenantContext.UserId, request, cancellationToken);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Join organization by join code.
    ///
    /// POST /api/orgs/join/code
    /// </summary>
    [HttpPost("join/code")]
    [ProducesResponseType(typeof(JoinOrganizationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<JoinOrganizationResponse>> JoinByCode(
        [FromBody] JoinOrgByCodeRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _orgService.JoinByCodeAsync(_tenantContext.UserId, request, cancellationToken);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
