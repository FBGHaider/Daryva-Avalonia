using Daryva.Api.Dtos;
using Daryva.Api.Security;
using Daryva.Api.Security.Interfaces;
using Daryva.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Daryva.Api.Controllers;

/// <summary>
/// API endpoints for tenant management.
/// All operations are scoped to the current organization context (X-Org-Id header).
/// </summary>
[ApiController]
[Route("api/tenants")]
[Authorize]
public class TenantsController : ControllerBase
{
    private readonly ITenantService _tenantService;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<TenantsController> _logger;

    public TenantsController(
        ITenantService tenantService,
        ITenantContext tenantContext,
        ILogger<TenantsController> logger)
    {
        _tenantService = tenantService;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    /// <summary>
    /// Get all tenants for the current organization.
    /// Requires X-Org-Id header or implicit org selection.
    ///
    /// GET /api/tenants?includeArchived=false
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<TenantResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [Authorize(Policy = Permissions.Tenants.View)]
    public async Task<ActionResult<IEnumerable<TenantResponse>>> GetTenants(
        [FromQuery] bool includeArchived = false,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.CurrentOrgId.HasValue)
            return BadRequest(new { error = "Organization context not set." });

        _logger.LogDebug("GetTenants called for organization {OrgId}, includeArchived={IncludeArchived}",
            _tenantContext.CurrentOrgId, includeArchived);

        var tenants = await _tenantService.GetAllTenantsAsync(includeArchived, cancellationToken);
        _logger.LogInformation("Retrieved {TenantCount} tenants for organization {OrgId}",
            tenants.Count, _tenantContext.CurrentOrgId);

        return Ok(tenants);
    }

    /// <summary>
    /// Get a specific tenant by ID.
    ///
    /// GET /api/tenants/{tenantId}
    /// </summary>
    [HttpGet("{tenantId:guid}")]
    [ProducesResponseType(typeof(TenantResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [Authorize(Policy = Permissions.Tenants.View)]
    public async Task<ActionResult<TenantResponse>> GetTenant(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.CurrentOrgId.HasValue)
            return BadRequest(new { error = "Organization context not set." });

        var tenant = await _tenantService.GetTenantByIdAsync(tenantId, cancellationToken);
        if (tenant == null)
            return NotFound();

        return Ok(tenant);
    }

    /// <summary>
    /// Create a new tenant in the current organization.
    ///
    /// POST /api/tenants
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(TenantResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [Authorize(Policy = Permissions.Tenants.Manage)]
    public async Task<ActionResult<TenantResponse>> CreateTenant(
        [FromBody] CreateTenantRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.CurrentOrgId.HasValue)
            return BadRequest(new { error = "Organization context not set." });

        try
        {
            var response = await _tenantService.CreateTenantAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetTenant), new { tenantId = response.Id }, response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Update an existing tenant.
    ///
    /// PUT /api/tenants/{tenantId}
    /// </summary>
    [HttpPut("{tenantId:guid}")]
    [ProducesResponseType(typeof(TenantResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [Authorize(Policy = Permissions.Tenants.Manage)]
    public async Task<ActionResult<TenantResponse>> UpdateTenant(
        Guid tenantId,
        [FromBody] UpdateTenantRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.CurrentOrgId.HasValue)
            return BadRequest(new { error = "Organization context not set." });

        var response = await _tenantService.UpdateTenantAsync(tenantId, request, cancellationToken);
        if (response == null)
            return NotFound();

        return Ok(response);
    }

    /// <summary>
    /// Archive a tenant (mark as left) and end any active tenancies.
    ///
    /// POST /api/tenants/{tenantId}/archive
    /// </summary>
    [HttpPost("{tenantId:guid}/archive")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [Authorize(Policy = Permissions.Tenants.Manage)]
    public async Task<IActionResult> ArchiveTenant(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.CurrentOrgId.HasValue)
            return BadRequest(new { error = "Organization context not set." });

        var archived = await _tenantService.ArchiveTenantAsync(tenantId, cancellationToken);
        if (!archived)
            return NotFound();

        return NoContent();
    }

    /// <summary>
    /// Unarchive a tenant.
    ///
    /// POST /api/tenants/{tenantId}/unarchive
    /// </summary>
    [HttpPost("{tenantId:guid}/unarchive")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [Authorize(Policy = Permissions.Tenants.Manage)]
    public async Task<IActionResult> UnarchiveTenant(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.CurrentOrgId.HasValue)
            return BadRequest(new { error = "Organization context not set." });

        var unarchived = await _tenantService.UnarchiveTenantAsync(tenantId, cancellationToken);
        if (!unarchived)
            return NotFound();

        return NoContent();
    }

    /// <summary>
    /// Delete a tenant.
    ///
    /// DELETE /api/tenants/{tenantId}
    /// </summary>
    [HttpDelete("{tenantId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [Authorize(Policy = Permissions.Tenants.Manage)]
    public async Task<IActionResult> DeleteTenant(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.CurrentOrgId.HasValue)
            return BadRequest(new { error = "Organization context not set." });

        var tenant = await _tenantService.GetTenantByIdAsync(tenantId, cancellationToken);
        if (tenant == null)
            return NotFound();

        await _tenantService.DeleteTenantAsync(tenantId, cancellationToken);

        return NoContent();
    }
}
