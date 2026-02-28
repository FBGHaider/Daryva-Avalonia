using Daryva.Api.Dtos;
using Daryva.Api.Security;
using Daryva.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Daryva.Api.Controllers;

/// <summary>
/// API endpoints for house (property) management.
/// All operations are scoped to the current organization context (X-Org-Id header).
/// </summary>
[ApiController]
[Route("api/houses")]
[Authorize]
public class HousesController : ControllerBase
{
    private readonly IHouseService _houseService;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<HousesController> _logger;

    public HousesController(
        IHouseService houseService,
        ITenantContext tenantContext,
        ILogger<HousesController> logger)
    {
        _houseService = houseService;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    /// <summary>
    /// Get all houses for the current organization.
    /// Requires X-Org-Id header or implicit org selection.
    ///
    /// GET /api/houses?includeArchived=false
    /// Headers:
    ///   X-Org-Id: 550e8400-e29b-41d4-a716-446655440000 (required if user belongs to multiple orgs)
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<HouseResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IEnumerable<HouseResponse>>> GetHouses(
        [FromQuery] bool includeArchived = false,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.CurrentOrgId.HasValue)
            return BadRequest(new { error = "Organization context not set." });

        var houses = await _houseService.GetHousesAsync(
            _tenantContext.CurrentOrgId.Value,
            includeArchived,
            cancellationToken);

        return Ok(houses);
    }

    /// <summary>
    /// Get a specific house by ID.
    ///
    /// GET /api/houses/{houseId}
    /// </summary>
    [HttpGet("{houseId}")]
    [ProducesResponseType(typeof(HouseResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<HouseResponse>> GetHouse(
        Guid houseId,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.CurrentOrgId.HasValue)
            return BadRequest(new { error = "Organization context not set." });

        var house = await _houseService.GetHouseAsync(
            _tenantContext.CurrentOrgId.Value,
            houseId,
            cancellationToken);

        if (house == null)
            return NotFound(new { error = "House not found." });

        return Ok(house);
    }

    /// <summary>
    /// Create a new house.
    /// OrganizationId is set server-side from current context; client input ignored.
    ///
    /// POST /api/houses
    /// {
    ///   "name": "Main St Apartment A",
    ///   "addressLine1": "123 Main Street",
    ///   "addressLine2": "Apt 1A",
    ///   "city": "New York",
    ///   "postcode": "10001"
    /// }
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(HouseResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<HouseResponse>> CreateHouse(
        [FromBody] CreateHouseRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.CurrentOrgId.HasValue)
            return BadRequest(new { error = "Organization context not set." });

        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var house = await _houseService.CreateHouseAsync(
                _tenantContext.CurrentOrgId.Value,
                request,
                cancellationToken);

            _logger.LogInformation(
                "User {UserId} created house {HouseId} in organization {OrgId}.",
                _tenantContext.UserId, house.Id, _tenantContext.CurrentOrgId.Value);

            return CreatedAtAction(nameof(GetHouse), new { houseId = house.Id }, house);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Update an existing house.
    /// Only non-null fields are updated.
    ///
    /// PUT /api/houses/{houseId}
    /// {
    ///   "name": "Main St Apartment A - Updated",
    ///   "city": "NYC"
    /// }
    /// </summary>
    [HttpPut("{houseId}")]
    [ProducesResponseType(typeof(HouseResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<HouseResponse>> UpdateHouse(
        Guid houseId,
        [FromBody] UpdateHouseRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.CurrentOrgId.HasValue)
            return BadRequest(new { error = "Organization context not set." });

        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var house = await _houseService.UpdateHouseAsync(
                _tenantContext.CurrentOrgId.Value,
                houseId,
                request,
                cancellationToken);

            if (house == null)
                return NotFound(new { error = "House not found." });

            _logger.LogInformation(
                "User {UserId} updated house {HouseId} in organization {OrgId}.",
                _tenantContext.UserId, houseId, _tenantContext.CurrentOrgId.Value);

            return Ok(house);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Archive a house (soft delete). Archived houses are excluded from GET unless includeArchived=true.
    ///
    /// POST /api/houses/{houseId}/archive
    /// </summary>
    [HttpPost("{houseId}/archive")]
    [ProducesResponseType(typeof(HouseResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<HouseResponse>> ArchiveHouse(
        Guid houseId,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.CurrentOrgId.HasValue)
            return BadRequest(new { error = "Organization context not set." });

        var house = await _houseService.ArchiveHouseAsync(
            _tenantContext.CurrentOrgId.Value,
            houseId,
            cancellationToken);

        if (house == null)
            return NotFound(new { error = "House not found." });

        _logger.LogInformation(
            "User {UserId} archived house {HouseId} in organization {OrgId}.",
            _tenantContext.UserId, houseId, _tenantContext.CurrentOrgId.Value);

        return Ok(house);
    }

    /// <summary>
    /// Delete a house permanently.
    ///
    /// DELETE /api/houses/{houseId}
    /// </summary>
    [HttpDelete("{houseId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> DeleteHouse(
        Guid houseId,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.CurrentOrgId.HasValue)
            return BadRequest(new { error = "Organization context not set." });

        var deleted = await _houseService.DeleteHouseAsync(
            _tenantContext.CurrentOrgId.Value,
            houseId,
            cancellationToken);

        if (!deleted)
            return NotFound(new { error = "House not found." });

        _logger.LogInformation(
            "User {UserId} deleted house {HouseId} from organization {OrgId}.",
            _tenantContext.UserId, houseId, _tenantContext.CurrentOrgId.Value);

        return NoContent();
    }
}
