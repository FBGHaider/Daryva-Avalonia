using Daryva.Api.Domain;
using Daryva.Api.Security;
using Daryva.Api.Services;
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
            tenants.Count(), _tenantContext.CurrentOrgId);

        var today = DateTime.UtcNow.Date;
        var response = tenants.Select(t => 
        {
            // Get current tenancy (active or most recent)
            var currentTenancy = t.Tenancies
                .Where(te => !te.MoveOutDate.HasValue || te.MoveOutDate >= today)
                .OrderByDescending(te => te.MoveInDate)
                .FirstOrDefault();
            
            return new TenantResponse
            {
                Id = t.Id,
                FullName = t.FullName,
                Email = t.Email,
                PhoneNumber = t.PhoneNumber,
                UniversityName = t.UniversityName,
                CreatedAt = t.CreatedAt,
                IsArchived = t.IsArchived,
                CurrentHouseAddress = currentTenancy?.House?.AddressLine1 ?? string.Empty,
                CurrentHouseId = currentTenancy?.HouseId,
                CurrentTenancyId = currentTenancy?.Id
            };
        }).ToList();

        return Ok(response);
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
    public async Task<ActionResult<TenantResponse>> GetTenant(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.CurrentOrgId.HasValue)
            return BadRequest(new { error = "Organization context not set." });

        var tenant = await _tenantService.GetTenantByIdAsync(tenantId);
        if (tenant == null)
            return NotFound();

        // Get current tenancy (active or most recent)
        var today = DateTime.UtcNow.Date;
        var currentTenancy = tenant.Tenancies
            .Where(te => !te.MoveOutDate.HasValue || te.MoveOutDate >= today)
            .OrderByDescending(te => te.MoveInDate)
            .FirstOrDefault();

        var response = new TenantResponse
        {
            Id = tenant.Id,
            FullName = tenant.FullName,
            Email = tenant.Email,
            PhoneNumber = tenant.PhoneNumber,
            UniversityName = tenant.UniversityName,
            CreatedAt = tenant.CreatedAt,
            IsArchived = tenant.IsArchived,
            CurrentHouseAddress = currentTenancy?.House?.AddressLine1 ?? string.Empty,
            CurrentHouseId = currentTenancy?.HouseId,
            CurrentTenancyId = currentTenancy?.Id
        };

        return Ok(response);
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
    public async Task<ActionResult<TenantResponse>> CreateTenant(
        [FromBody] CreateTenantRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.CurrentOrgId.HasValue)
            return BadRequest(new { error = "Organization context not set." });

        if (string.IsNullOrWhiteSpace(request.FullName))
            return BadRequest(new { error = "Full name is required." });

        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            OrganizationId = _tenantContext.CurrentOrgId.Value,
            FullName = request.FullName,
            Email = request.Email ?? string.Empty,
            PhoneNumber = request.PhoneNumber ?? string.Empty,
            CreatedAt = DateTime.UtcNow,
            IsArchived = false
        };

        var createdTenant = await _tenantService.CreateTenantAsync(tenant);

        var response = new TenantResponse
        {
            Id = createdTenant.Id,
            FullName = createdTenant.FullName,
            Email = createdTenant.Email,
            PhoneNumber = createdTenant.PhoneNumber,
            CreatedAt = createdTenant.CreatedAt,
            IsArchived = createdTenant.IsArchived
        };

        return CreatedAtAction(nameof(GetTenant), new { tenantId = createdTenant.Id }, response);
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
    public async Task<ActionResult<TenantResponse>> UpdateTenant(
        Guid tenantId,
        [FromBody] UpdateTenantRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.CurrentOrgId.HasValue)
            return BadRequest(new { error = "Organization context not set." });

        var tenant = await _tenantService.GetTenantByIdAsync(tenantId);
        if (tenant == null)
            return NotFound();

        tenant.FullName = request.FullName ?? tenant.FullName;
        tenant.Email = request.Email ?? tenant.Email;
        tenant.PhoneNumber = request.PhoneNumber ?? tenant.PhoneNumber;

        await _tenantService.UpdateTenantAsync(tenant);

        var response = new TenantResponse
        {
            Id = tenant.Id,
            FullName = tenant.FullName,
            Email = tenant.Email,
            PhoneNumber = tenant.PhoneNumber,
            CreatedAt = tenant.CreatedAt,
            IsArchived = tenant.IsArchived
        };

        return Ok(response);
    }

    /// <summary>
    /// Archive a tenant.
    ///
    /// POST /api/tenants/{tenantId}/archive
    /// </summary>
    [HttpPost("{tenantId:guid}/archive")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ArchiveTenant(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.CurrentOrgId.HasValue)
            return BadRequest(new { error = "Organization context not set." });

        var tenant = await _tenantService.GetTenantByIdAsync(tenantId);
        if (tenant == null)
            return NotFound();

        tenant.IsArchived = true;
        await _tenantService.UpdateTenantAsync(tenant);

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
    public async Task<IActionResult> UnarchiveTenant(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.CurrentOrgId.HasValue)
            return BadRequest(new { error = "Organization context not set." });

        var tenant = await _tenantService.GetTenantByIdAsync(tenantId);
        if (tenant == null)
            return NotFound();

        tenant.IsArchived = false;
        await _tenantService.UpdateTenantAsync(tenant);

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
    public async Task<IActionResult> DeleteTenant(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.CurrentOrgId.HasValue)
            return BadRequest(new { error = "Organization context not set." });

        var tenant = await _tenantService.GetTenantByIdAsync(tenantId);
        if (tenant == null)
            return NotFound();

        await _tenantService.DeleteTenantAsync(tenantId);

        return NoContent();
    }
}

public class CreateTenantRequest
{
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
}

public class UpdateTenantRequest
{
    public string? FullName { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
}

public class TenantResponse
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? UniversityName { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsArchived { get; set; }
    
    // House/Tenancy information
    public string? CurrentHouseAddress { get; set; }
    public Guid? CurrentHouseId { get; set; }
    public Guid? CurrentTenancyId { get; set; }
}
