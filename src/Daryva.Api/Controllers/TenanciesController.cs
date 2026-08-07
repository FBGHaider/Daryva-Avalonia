using Daryva.Api.Dtos;
using Daryva.Api.Security;
using Daryva.Api.Security.Interfaces;
using Daryva.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Daryva.Api.Controllers;

[ApiController]
[Route("api/tenancies")]
[Authorize]
public class TenanciesController : ControllerBase
{
    private readonly ITenancyService _tenancyService;
    private readonly ITenantContext _tenantContext;

    public TenanciesController(ITenancyService tenancyService, ITenantContext tenantContext)
    {
        _tenancyService = tenancyService;
        _tenantContext = tenantContext;
    }

    [HttpGet]
    [Authorize(Policy = Permissions.Tenancies.View)]
    public async Task<ActionResult<IEnumerable<TenancyDetailResponse>>> GetTenancies(
        [FromQuery] Guid? tenantId,
        [FromQuery] Guid? houseId,
        [FromQuery] bool? activeOnly,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.CurrentOrgId.HasValue)
            return BadRequest(new { error = "Organization context not set." });

        // A Tenant caller only ever sees their own tenancy -- ignore any client-supplied
        // tenantId and force their own, regardless of what was requested.
        if (_tenantContext.CurrentRole == Roles.Tenant)
            tenantId = _tenantContext.CurrentTenantId ?? Guid.Empty;

        try
        {
            var tenancies = await _tenancyService.GetTenanciesAsync(tenantId, houseId, activeOnly, cancellationToken);
            return Ok(tenancies);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Failed to load tenancies.", detail = ex.Message });
        }
    }

    [HttpGet("active-in-period")]
    [Authorize(Policy = Permissions.Tenancies.View)]
    public async Task<ActionResult<IEnumerable<TenancyDetailResponse>>> GetTenanciesActiveInPeriod(
        [FromQuery] int year,
        [FromQuery] int month,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.CurrentOrgId.HasValue)
            return BadRequest(new { error = "Organization context not set." });

        var tenancies = await _tenancyService.GetTenanciesActiveInPeriodAsync(year, month, cancellationToken);
        return Ok(tenancies.Where(IsVisibleToCaller));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(TenancyDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Authorize(Policy = Permissions.Tenancies.View)]
    public async Task<ActionResult<TenancyDetailResponse>> GetTenancy(Guid id, CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.CurrentOrgId.HasValue)
            return BadRequest(new { error = "Organization context not set." });

        try
        {
            var tenancy = await _tenancyService.GetTenancyAsync(id, cancellationToken);
            if (tenancy == null || !IsVisibleToCaller(tenancy))
                return NotFound();

            return Ok(tenancy);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Failed to load tenancy.", detail = ex.Message });
        }
    }

    [HttpGet("ended-with-deposit")]
    [Authorize(Policy = Permissions.Tenancies.View)]
    public async Task<ActionResult<IEnumerable<TenancyDetailResponse>>> GetEndedTenanciesWithDeposit(CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.CurrentOrgId.HasValue)
            return BadRequest(new { error = "Organization context not set." });

        var tenancies = await _tenancyService.GetEndedTenanciesWithDepositAsync(cancellationToken);
        return Ok(tenancies.Where(IsVisibleToCaller));
    }

    /// <summary>Same isolation rationale as DocumentsController.IsVisibleToCaller.</summary>
    private bool IsVisibleToCaller(TenancyDetailResponse tenancy)
        => _tenantContext.CurrentRole != Roles.Tenant || tenancy.TenantId == _tenantContext.CurrentTenantId;

    [HttpPatch("{id:guid}/end")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Authorize(Policy = Permissions.Tenancies.Manage)]
    public async Task<IActionResult> EndTenancy(Guid id, [FromBody] EndTenancyRequest request, CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.CurrentOrgId.HasValue)
            return BadRequest(new { error = "Organization context not set." });

        try
        {
            var found = await _tenancyService.EndTenancyAsync(id, request.MoveOutDate, cancellationToken);
            if (!found)
                return NotFound();
            return NoContent();
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Failed to end tenancy.", detail = ex.Message });
        }
    }

    [HttpPatch("{id:guid}/reactivate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Authorize(Policy = Permissions.Tenancies.Manage)]
    public async Task<IActionResult> ReactivateTenancy(Guid id, CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.CurrentOrgId.HasValue)
            return BadRequest(new { error = "Organization context not set." });

        var found = await _tenancyService.ReactivateTenancyAsync(id, cancellationToken);
        if (!found)
            return NotFound();
        return NoContent();
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [Authorize(Policy = Permissions.Tenancies.Manage)]
    public async Task<IActionResult> UpdateTenancy(Guid id, [FromBody] UpdateTenancyRequest request, CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.CurrentOrgId.HasValue)
            return BadRequest(new { error = "Organization context not set." });

        try
        {
            var found = await _tenancyService.UpdateTenancyAsync(id, request, cancellationToken);
            if (!found)
                return NotFound();
            return NoContent();
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Failed to save tenancy.", detail = ex.Message, inner = ex.InnerException?.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Authorize(Policy = Permissions.Tenancies.Manage)]
    public async Task<IActionResult> DeleteTenancy(Guid id, CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.CurrentOrgId.HasValue)
            return BadRequest(new { error = "Organization context not set." });

        var found = await _tenancyService.DeleteTenancyAsync(id, cancellationToken);
        if (!found)
            return NotFound();
        return NoContent();
    }

    [HttpDelete("by-house/{houseId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [Authorize(Policy = Permissions.Tenancies.Manage)]
    public async Task<IActionResult> DeleteEndedTenanciesByHouse(Guid houseId, [FromQuery] bool endedOnly = true, CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.CurrentOrgId.HasValue)
            return BadRequest(new { error = "Organization context not set." });

        await _tenancyService.DeleteEndedTenanciesByHouseAsync(houseId, endedOnly, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Create a tenancy (assign tenant to house with rent details).
    /// POST /api/tenancies
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(CreateTenancyResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Authorize(Policy = Permissions.Tenancies.Manage)]
    public async Task<ActionResult<CreateTenancyResponse>> CreateTenancy(
        [FromBody] CreateTenancyRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.CurrentOrgId.HasValue)
            return BadRequest(new { error = "Organization context not set." });

        try
        {
            var id = await _tenancyService.CreateTenancyAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetTenancies), new CreateTenancyResponse { Id = id });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Failed to create tenancy.", detail = ex.Message });
        }
    }

    /// <summary>
    /// Export tenancies with current rent/deposit for editing and then calling repair-rent.
    /// GET /api/tenancies/export-for-rent-repair
    /// </summary>
    [HttpGet("export-for-rent-repair")]
    [ProducesResponseType(typeof(IEnumerable<RentRepairExportItem>), StatusCodes.Status200OK)]
    [Authorize(Policy = Permissions.Tenancies.View)]
    public async Task<ActionResult<IEnumerable<RentRepairExportItem>>> ExportForRentRepair(CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.CurrentOrgId.HasValue)
            return BadRequest(new { error = "Organization context not set." });

        var list = await _tenancyService.ExportForRentRepairAsync(cancellationToken);
        return Ok(list);
    }

    /// <summary>
    /// Update only rent (and optionally deposit) for tenancies. Use to fix wrong values (e.g. after migration).
    /// POST /api/tenancies/repair-rent
    /// </summary>
    [HttpPost("repair-rent")]
    [ProducesResponseType(typeof(RentRepairResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [Authorize(Policy = Permissions.Tenancies.Manage)]
    public async Task<ActionResult<RentRepairResult>> RepairRent(
        [FromBody] RentRepairRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.CurrentOrgId.HasValue)
            return BadRequest(new { error = "Organization context not set." });
        if (request.Updates == null || request.Updates.Count == 0)
            return BadRequest(new { error = "At least one update is required." });

        var result = await _tenancyService.RepairRentAsync(request, cancellationToken);
        return Ok(result);
    }
}
