using Daryva.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Daryva.Api.Controllers;

/// <summary>
/// Diagnostic endpoints for debugging data issues.
/// DEVELOPMENT ONLY - should be removed in production.
/// Bypasses organization filtering to show raw data counts.
/// </summary>
[ApiController]
[Route("api/diagnostic")]
public class DiagnosticController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DiagnosticController> _logger;

    public DiagnosticController(AppDbContext dbContext, IConfiguration configuration, ILogger<DiagnosticController> logger)
    {
        _dbContext = dbContext;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Get raw data counts without organization filtering.
    /// Development only.
    /// </summary>
    [HttpGet("data-counts")]
    public async Task<ActionResult> GetDataCounts()
    {
        // Check if development mode
        var devAuthEnabled = _configuration.GetValue<bool>("DevAuth:Enabled");
        if (!devAuthEnabled)
        {
            return BadRequest(new { error = "Diagnostic endpoints disabled. DevAuth not enabled." });
        }

        try
        {
            // Bypass global query filters by using IgnoreQueryFilters
            var organizations = await _dbContext.Organizations
                .IgnoreQueryFilters()
                .ToListAsync();

            var houses = await _dbContext.Houses
                .IgnoreQueryFilters()
                .ToListAsync();

            var tenants = await _dbContext.Tenants
                .IgnoreQueryFilters()
                .ToListAsync();

            var tenancies = await _dbContext.Tenancies
                .IgnoreQueryFilters()
                .ToListAsync();

            var expenses = await _dbContext.Expenses
                .IgnoreQueryFilters()
                .ToListAsync();

            var documents = await _dbContext.Documents
                .IgnoreQueryFilters()
                .ToListAsync();

            var rentPayments = await _dbContext.RentPayments
                .IgnoreQueryFilters()
                .ToListAsync();

            var depositPayments = await _dbContext.DepositPayments
                .IgnoreQueryFilters()
                .ToListAsync();

            return Ok(new
            {
                message = "Raw data counts (bypassing organization filters)",
                counts = new
                {
                    organizations = organizations.Count,
                    houses = houses.Count,
                    tenants = tenants.Count,
                    tenancies = tenancies.Count,
                    expenses = expenses.Count,
                    documents = documents.Count,
                    rentPayments = rentPayments.Count,
                    depositPayments = depositPayments.Count
                },
                organizations = organizations.Select(o => new
                {
                    id = o.Id,
                    name = o.Name,
                    createdAt = o.CreatedAt
                }),
                houseSummary = houses.GroupBy(h => h.OrganizationId).Select(g => new
                {
                    organizationId = g.Key,
                    count = g.Count(),
                    names = g.Select(h => h.Name).ToList()
                }),
                tenantSummary = tenants.GroupBy(t => t.OrganizationId).Select(g => new
                {
                    organizationId = g.Key,
                    count = g.Count(),
                    names = g.Select(t => t.FullName).ToList()
                })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting diagnostic data counts");
            return StatusCode(500, new { error = "Error retrieving data", message = ex.Message });
        }
    }

    /// <summary>
    /// Get organization memberships.
    /// Development only.
    /// </summary>
    [HttpGet("org-members")]
    public async Task<ActionResult> GetOrgMembers()
    {
        // Check if development mode
        var devAuthEnabled = _configuration.GetValue<bool>("DevAuth:Enabled");
        if (!devAuthEnabled)
        {
            return BadRequest(new { error = "Diagnostic endpoints disabled. DevAuth not enabled." });
        }

        try
        {
            var members = await _dbContext.OrganizationMembers
                .IgnoreQueryFilters()
                .Include(m => m.Organization)
                .ToListAsync();

            return Ok(new
            {
                message = "All organization memberships",
                members = members.Select(m => new
                {
                    userId = m.UserId,
                    organizationId = m.OrganizationId,
                    organizationName = m.Organization?.Name,
                    role = m.Role,
                    joinedAt = m.JoinedAt
                })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting org members");
            return StatusCode(500, new { error = "Error retrieving data", message = ex.Message });
        }
    }
}
