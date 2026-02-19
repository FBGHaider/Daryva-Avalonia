using Daryva.Api.Data;
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
    public async Task<ActionResult<IEnumerable<TenancyLookupResponse>>> GetTenancies(CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.CurrentOrgId.HasValue)
            return BadRequest(new { error = "Organization context not set." });

        var orgId = _tenantContext.CurrentOrgId.Value;
        var tenancies = await _dbContext.Tenancies
            .AsNoTracking()
            .Where(t => t.OrganizationId == orgId)
            .Select(t => new TenancyLookupResponse
            {
                Id = t.Id,
                TenantId = t.TenantId,
                HouseId = t.HouseId,
                MoveOutDate = t.MoveOutDate,
                Status = t.Status
            })
            .ToListAsync(cancellationToken);

        return Ok(tenancies);
    }
}

public class TenancyLookupResponse
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid HouseId { get; set; }
    public DateTime? MoveOutDate { get; set; }
    public string Status { get; set; } = string.Empty;
}
