using Daryva.Api.Data;
using Daryva.Api.Domain;
using Daryva.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Daryva.Api.Controllers;

[ApiController]
[Route("api/tenancies")]
[Authorize]
public class TenanciesController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly ITenantContext _tenantContext;

    public TenanciesController(AppDbContext dbContext, ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<TenancyDetailResponse>>> GetTenancies(
        [FromQuery] Guid? tenantId,
        [FromQuery] Guid? houseId,
        [FromQuery] bool? activeOnly,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.CurrentOrgId.HasValue)
            return BadRequest(new { error = "Organization context not set." });

        var orgId = _tenantContext.CurrentOrgId.Value;
        try
        {
            var query = _dbContext.Tenancies.AsNoTracking().Include(t => t.House).Include(t => t.Tenant).Where(t => t.OrganizationId == orgId);
            if (tenantId.HasValue)
                query = query.Where(t => t.TenantId == tenantId.Value);
            if (houseId.HasValue)
                query = query.Where(t => t.HouseId == houseId.Value);
            if (activeOnly == true)
                query = query.Where(t => t.Status == "Active");

            var tenancies = await query.OrderByDescending(t => t.MoveInDate).ToListAsync(cancellationToken);
            return Ok(tenancies.Select(MapToDetailResponse));
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Failed to load tenancies.", detail = ex.Message });
        }
    }

    [HttpGet("active-in-period")]
    public async Task<ActionResult<IEnumerable<TenancyDetailResponse>>> GetTenanciesActiveInPeriod(
        [FromQuery] int year,
        [FromQuery] int month,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.CurrentOrgId.HasValue)
            return BadRequest(new { error = "Organization context not set." });

        var orgId = _tenantContext.CurrentOrgId.Value;
        var periodStart = new DateTime(year, month, 1);
        var periodEnd = periodStart.AddMonths(1);

        var tenancies = await _dbContext.Tenancies
            .AsNoTracking()
            .Include(t => t.House)
            .Include(t => t.Tenant)
            .Where(t => t.OrganizationId == orgId && t.MoveInDate < periodEnd && (t.MoveOutDate == null || t.MoveOutDate >= periodStart))
            .OrderByDescending(t => t.MoveInDate)
            .ToListAsync(cancellationToken);

        return Ok(tenancies.Select(MapToDetailResponse));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(TenancyDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TenancyDetailResponse>> GetTenancy(Guid id, CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.CurrentOrgId.HasValue)
            return BadRequest(new { error = "Organization context not set." });

        var orgId = _tenantContext.CurrentOrgId.Value;
        try
        {
            var tenancy = await _dbContext.Tenancies
                .AsNoTracking()
                .Include(t => t.House)
                .Include(t => t.Tenant)
                .FirstOrDefaultAsync(t => t.Id == id && t.OrganizationId == orgId, cancellationToken);
            if (tenancy == null)
                return NotFound();

            return Ok(MapToDetailResponse(tenancy));
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Failed to load tenancy.", detail = ex.Message });
        }
    }

    [HttpGet("ended-with-deposit")]
    public async Task<ActionResult<IEnumerable<TenancyDetailResponse>>> GetEndedTenanciesWithDeposit(CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.CurrentOrgId.HasValue)
            return BadRequest(new { error = "Organization context not set." });

        var orgId = _tenantContext.CurrentOrgId.Value;
        var tenancies = await _dbContext.Tenancies
            .AsNoTracking()
            .Include(t => t.House)
            .Include(t => t.Tenant)
            .Where(t => t.OrganizationId == orgId && t.Status == "Ended" && t.DepositAmount > 0 && t.MoveOutDate != null)
            .OrderByDescending(t => t.MoveOutDate)
            .ToListAsync(cancellationToken);

        return Ok(tenancies.Select(MapToDetailResponse));
    }

    [HttpPatch("{id:guid}/end")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> EndTenancy(Guid id, [FromBody] EndTenancyRequest request, CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.CurrentOrgId.HasValue)
            return BadRequest(new { error = "Organization context not set." });

        try
        {
            var orgId = _tenantContext.CurrentOrgId.Value;
            var tenancy = await _dbContext.Tenancies
                .FirstOrDefaultAsync(t => t.Id == id && t.OrganizationId == orgId, cancellationToken);
            if (tenancy == null)
                return NotFound();

            var moveOutUtc = request.MoveOutDate.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(request.MoveOutDate, DateTimeKind.Utc)
                : request.MoveOutDate.ToUniversalTime();
            tenancy.MoveOutDate = moveOutUtc;
            tenancy.Status = "Ended";
            await _dbContext.SaveChangesAsync(cancellationToken);
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
    public async Task<IActionResult> ReactivateTenancy(Guid id, CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.CurrentOrgId.HasValue)
            return BadRequest(new { error = "Organization context not set." });

        var orgId = _tenantContext.CurrentOrgId.Value;
        var tenancy = await _dbContext.Tenancies
            .FirstOrDefaultAsync(t => t.Id == id && t.OrganizationId == orgId, cancellationToken);
        if (tenancy == null)
            return NotFound();

        tenancy.MoveOutDate = null;
        tenancy.Status = "Active";
        await _dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateTenancy(Guid id, [FromBody] UpdateTenancyRequest request, CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.CurrentOrgId.HasValue)
            return BadRequest(new { error = "Organization context not set." });

        var orgId = _tenantContext.CurrentOrgId.Value;
        var tenancy = await _dbContext.Tenancies
            .FirstOrDefaultAsync(t => t.Id == id && t.OrganizationId == orgId, cancellationToken);
        if (tenancy == null)
            return NotFound();

        var moveIn = request.MoveInDate.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(request.MoveInDate.Date, DateTimeKind.Utc)
            : request.MoveInDate.ToUniversalTime();
        var moveOut = request.MoveOutDate.HasValue
            ? (DateTime?) (request.MoveOutDate.Value.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(request.MoveOutDate.Value.Date, DateTimeKind.Utc)
                : request.MoveOutDate.Value.ToUniversalTime())
            : null;

        tenancy.MoveInDate = moveIn;
        tenancy.MoveOutDate = moveOut;
        tenancy.RentStartMonth = request.RentStartMonth;
        tenancy.RentStartYear = request.RentStartYear;
        tenancy.RentAmountMonthly = request.RentAmountMonthly;
        tenancy.DepositAmount = request.DepositAmount;
        tenancy.PaymentDueDay = request.PaymentDueDay;
        tenancy.Status = string.IsNullOrWhiteSpace(request.Status) ? (tenancy.Status ?? "Active") : request.Status.Trim();
        tenancy.Notes = request.Notes;

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
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
    public async Task<IActionResult> DeleteTenancy(Guid id, CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.CurrentOrgId.HasValue)
            return BadRequest(new { error = "Organization context not set." });

        var orgId = _tenantContext.CurrentOrgId.Value;
        var tenancy = await _dbContext.Tenancies
            .FirstOrDefaultAsync(t => t.Id == id && t.OrganizationId == orgId, cancellationToken);
        if (tenancy == null)
            return NotFound();

        _dbContext.Tenancies.Remove(tenancy);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpDelete("by-house/{houseId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteEndedTenanciesByHouse(Guid houseId, [FromQuery] bool endedOnly = true, CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.CurrentOrgId.HasValue)
            return BadRequest(new { error = "Organization context not set." });

        var orgId = _tenantContext.CurrentOrgId.Value;
        var query = _dbContext.Tenancies.Where(t => t.HouseId == houseId && t.OrganizationId == orgId);
        if (endedOnly)
            query = query.Where(t => t.Status == "Ended");
        var toRemove = await query.ToListAsync(cancellationToken);
        _dbContext.Tenancies.RemoveRange(toRemove);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private static TenancyDetailResponse MapToDetailResponse(Tenancy t)
    {
        return new TenancyDetailResponse
        {
            Id = t.Id,
            HouseId = t.HouseId,
            TenantId = t.TenantId,
            MoveInDate = t.MoveInDate,
            MoveOutDate = t.MoveOutDate,
            RentStartMonth = t.RentStartMonth,
            RentStartYear = t.RentStartYear,
            RentAmountMonthly = t.RentAmountMonthly,
            DepositAmount = t.DepositAmount,
            PaymentDueDay = t.PaymentDueDay,
            Status = t.Status ?? "Active",
            Notes = t.Notes,
            House = t.House == null ? null : new TenancyHouseDto { Id = t.House.Id, AddressLine1 = t.House.AddressLine1, AddressLine2 = t.House.AddressLine2, City = t.House.City, Postcode = t.House.Postcode, TotalRooms = t.House.TotalRooms, CreatedAt = t.House.CreatedAt },
            Tenant = t.Tenant == null ? null : new TenancyTenantDto { Id = t.Tenant.Id, FullName = t.Tenant.FullName, PhoneNumber = t.Tenant.PhoneNumber, Email = t.Tenant.Email, UniversityName = t.Tenant.UniversityName, CreatedAt = t.Tenant.CreatedAt, IsArchived = t.Tenant.IsArchived }
        };
    }

    /// <summary>
    /// Create a tenancy (assign tenant to house with rent details).
    /// POST /api/tenancies
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(CreateTenancyResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CreateTenancyResponse>> CreateTenancy(
        [FromBody] CreateTenancyRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.CurrentOrgId.HasValue)
            return BadRequest(new { error = "Organization context not set." });

        if (request.RentAmountMonthly <= 0)
            return BadRequest(new { error = "Rent amount must be greater than 0." });
        if (request.PaymentDueDay < 1 || request.PaymentDueDay > 31)
            return BadRequest(new { error = "Payment due day must be between 1 and 31." });

        var moveInDate = request.MoveInDate.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(request.MoveInDate.Date, DateTimeKind.Utc)
            : request.MoveInDate.ToUniversalTime();
        if (moveInDate.Year < 2000 || moveInDate.Year > 2100)
            return BadRequest(new { error = "Move-in date must be between 2000 and 2100." });

        var orgId = _tenantContext.CurrentOrgId.Value;

        var house = await _dbContext.Houses
            .AsNoTracking()
            .FirstOrDefaultAsync(h => h.Id == request.HouseId && h.OrganizationId == orgId, cancellationToken);
        if (house == null)
            return NotFound(new { error = "House not found." });

        var tenant = await _dbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == request.TenantId && t.OrganizationId == orgId, cancellationToken);
        if (tenant == null)
            return NotFound(new { error = "Tenant not found." });

        try
        {
            var tenancy = new Tenancy
            {
                Id = Guid.NewGuid(),
                OrganizationId = orgId,
                HouseId = request.HouseId,
                TenantId = request.TenantId,
                MoveInDate = moveInDate,
                MoveOutDate = request.MoveOutDate,
                RentStartMonth = request.RentStartMonth,
                RentStartYear = request.RentStartYear,
                RentAmountMonthly = request.RentAmountMonthly,
                DepositAmount = request.DepositAmount,
                PaymentDueDay = request.PaymentDueDay,
                Status = request.Status ?? "Active",
                Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim()
            };

            _dbContext.Tenancies.Add(tenancy);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return CreatedAtAction(nameof(GetTenancies), new CreateTenancyResponse { Id = tenancy.Id });
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
    public async Task<ActionResult<IEnumerable<RentRepairExportItem>>> ExportForRentRepair(CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.CurrentOrgId.HasValue)
            return BadRequest(new { error = "Organization context not set." });

        var orgId = _tenantContext.CurrentOrgId.Value;
        var list = await _dbContext.Tenancies
            .AsNoTracking()
            .Include(t => t.House)
            .Include(t => t.Tenant)
            .Where(t => t.OrganizationId == orgId)
            .OrderBy(t => t.Tenant!.FullName)
            .ThenBy(t => t.House!.AddressLine1)
            .Select(t => new RentRepairExportItem
            {
                TenancyId = t.Id,
                TenantName = t.Tenant!.FullName,
                HouseName = t.House!.Name,
                RentAmountMonthly = t.RentAmountMonthly,
                DepositAmount = t.DepositAmount
            })
            .ToListAsync(cancellationToken);

        return Ok(list);
    }

    /// <summary>
    /// Update only rent (and optionally deposit) for tenancies. Use to fix wrong values (e.g. after migration).
    /// POST /api/tenancies/repair-rent
    /// </summary>
    [HttpPost("repair-rent")]
    [ProducesResponseType(typeof(RentRepairResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RentRepairResult>> RepairRent(
        [FromBody] RentRepairRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.CurrentOrgId.HasValue)
            return BadRequest(new { error = "Organization context not set." });
        if (request.Updates == null || request.Updates.Count == 0)
            return BadRequest(new { error = "At least one update is required." });

        var orgId = _tenantContext.CurrentOrgId.Value;
        var tenancyIds = request.Updates.Select(u => u.TenancyId).Distinct().ToList();
        var tenancies = await _dbContext.Tenancies
            .Where(t => t.OrganizationId == orgId && tenancyIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, cancellationToken);

        var updated = 0;
        var errors = new List<string>();

        foreach (var u in request.Updates)
        {
            if (u.RentAmountMonthly <= 0)
            {
                errors.Add($"Tenancy {u.TenancyId}: RentAmountMonthly must be greater than 0.");
                continue;
            }

            if (!tenancies.TryGetValue(u.TenancyId, out var tenancy))
            {
                errors.Add($"Tenancy {u.TenancyId}: Not found or not in this organization.");
                continue;
            }

            tenancy.RentAmountMonthly = u.RentAmountMonthly;
            if (u.DepositAmount.HasValue && u.DepositAmount.Value >= 0)
                tenancy.DepositAmount = u.DepositAmount.Value;
            updated++;
        }

        if (updated > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return Ok(new RentRepairResult { UpdatedCount = updated, Errors = errors });
    }
}

public class RentRepairExportItem
{
    public Guid TenancyId { get; set; }
    public string TenantName { get; set; } = string.Empty;
    public string HouseName { get; set; } = string.Empty;
    public decimal RentAmountMonthly { get; set; }
    public decimal DepositAmount { get; set; }
}

public class RentRepairRequest
{
    public List<RentRepairUpdateItem> Updates { get; set; } = new();
}

public class RentRepairUpdateItem
{
    public Guid TenancyId { get; set; }
    public decimal RentAmountMonthly { get; set; }
    public decimal? DepositAmount { get; set; }
}

public class RentRepairResult
{
    public int UpdatedCount { get; set; }
    public List<string> Errors { get; set; } = new();
}

public class TenancyLookupResponse
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid HouseId { get; set; }
    public DateTime? MoveOutDate { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class CreateTenancyRequest
{
    public Guid HouseId { get; set; }
    public Guid TenantId { get; set; }
    public DateTime MoveInDate { get; set; }
    public DateTime? MoveOutDate { get; set; }
    public int? RentStartMonth { get; set; }
    public int? RentStartYear { get; set; }
    public decimal RentAmountMonthly { get; set; }
    public decimal DepositAmount { get; set; }
    public byte PaymentDueDay { get; set; }
    public string? Status { get; set; }
    public string? Notes { get; set; }
}

public class CreateTenancyResponse
{
    public Guid Id { get; set; }
}

public class TenancyDetailResponse
{
    public Guid Id { get; set; }
    public Guid HouseId { get; set; }
    public Guid TenantId { get; set; }
    public DateTime MoveInDate { get; set; }
    public DateTime? MoveOutDate { get; set; }
    public int? RentStartMonth { get; set; }
    public int? RentStartYear { get; set; }
    public decimal RentAmountMonthly { get; set; }
    public decimal DepositAmount { get; set; }
    public byte PaymentDueDay { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public TenancyHouseDto? House { get; set; }
    public TenancyTenantDto? Tenant { get; set; }
}

public class TenancyHouseDto
{
    public Guid Id { get; set; }
    public string AddressLine1 { get; set; } = string.Empty;
    public string? AddressLine2 { get; set; }
    public string City { get; set; } = string.Empty;
    public string Postcode { get; set; } = string.Empty;
    public int TotalRooms { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class TenancyTenantDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? UniversityName { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsArchived { get; set; }
}

public class EndTenancyRequest
{
    public DateTime MoveOutDate { get; set; }
}

public class UpdateTenancyRequest
{
    public DateTime MoveInDate { get; set; }
    public DateTime? MoveOutDate { get; set; }
    public int? RentStartMonth { get; set; }
    public int? RentStartYear { get; set; }
    public decimal RentAmountMonthly { get; set; }
    public decimal DepositAmount { get; set; }
    public byte PaymentDueDay { get; set; }
    public string? Status { get; set; }
    public string? Notes { get; set; }
}
