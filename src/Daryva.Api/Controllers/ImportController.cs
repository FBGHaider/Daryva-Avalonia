using Daryva.Api.Dtos;
using Daryva.Api.Security;
using Daryva.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Daryva.Api.Controllers;

/// <summary>
/// Bulk data import endpoint for migrating from SQLite.
/// DevAuth only.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ImportController : ControllerBase
{
    private readonly IBulkImportService _bulkImportService;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<ImportController> _logger;
    private readonly IConfiguration _configuration;

    public ImportController(
        IBulkImportService bulkImportService,
        ITenantContext tenantContext,
        ILogger<ImportController> logger,
        IConfiguration configuration)
    {
        _bulkImportService = bulkImportService;
        _tenantContext = tenantContext;
        _logger = logger;
        _configuration = configuration;
    }

    /// <summary>
    /// Bulk import data from SQLite.
    /// 
    /// Imports all data (houses, tenants, tenancies, expenses, documents, payments)
    /// into the current organization.
    /// 
    /// POST /api/import
    /// Headers: X-Org-Id (required)
    /// Body: BulkImportRequest JSON
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<BulkImportResponse>> BulkImport(
        [FromBody] BulkImportRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Verify organization context (user must be member of org; middleware validates)
            if (!_tenantContext.CurrentOrgId.HasValue)
                return BadRequest(new { error = "Organization context not set. Specify X-Org-Id header." });

            _logger.LogInformation(
                "Starting bulk import for org {OrgId}: {Houses} houses, {Tenants} tenants, {Tenancies} tenancies, {Expenses} expenses, {Documents} documents, {RentPayments} rent payments, {DepositPayments} deposit payments, {NotificationTemplates} templates, {Notifications} notifications, {NotificationAttempts} attempts",
                _tenantContext.CurrentOrgId.Value,
                request.Houses.Count,
                request.Tenants.Count,
                request.Tenancies.Count,
                request.Expenses.Count,
                request.Documents.Count,
                request.RentPayments.Count,
                request.DepositPayments.Count,
                request.NotificationTemplates.Count,
                request.Notifications.Count,
                request.NotificationAttempts.Count);

            var response = await _bulkImportService.ImportDataAsync(
                _tenantContext.CurrentOrgId.Value,
                request,
                cancellationToken);

            if (!response.Success)
            {
                return BadRequest(response);
            }

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Import failed");
            return StatusCode(500, new { error = "Import failed", message = ex.Message });
        }
    }

    /// <summary>
    /// Clear all imported data for the current organization.
    /// USE WITH CAUTION - this deletes ALL data for the organization!
    /// 
    /// DELETE /api/import/clear
    /// Headers: X-Org-Id (required)
    /// </summary>
    [HttpDelete("clear")]
    public async Task<ActionResult> ClearAllData(CancellationToken cancellationToken = default)
    {
        try
        {
            // Verify organization context
            if (!_tenantContext.CurrentOrgId.HasValue)
                return BadRequest(new { error = "Organization context not set. Specify X-Org-Id header." });

            var devAuthEnabled = _configuration.GetValue<bool>("DevAuth:Enabled");
            if (!devAuthEnabled)
            {
                _logger.LogWarning("Clear endpoint called when DevAuth is disabled");
                return BadRequest(new { error = "Clear endpoint is only available in development mode (DevAuth)" });
            }

            var orgId = _tenantContext.CurrentOrgId.Value;
            _logger.LogWarning("Clearing ALL data for organization {OrgId}", orgId);

            // Get the DbContext
            var dbContext = HttpContext.RequestServices.GetRequiredService<Daryva.Api.Data.AppDbContext>();

            // Delete in reverse order of dependencies using LINQ
            // Load and remove notification attempts
            var notificationAttempts = await dbContext.NotificationAttempts
                .Where(n => n.OrganizationId == orgId)
                .ToListAsync(cancellationToken);
            dbContext.NotificationAttempts.RemoveRange(notificationAttempts);

            // Load and remove notifications
            var notifications = await dbContext.Notifications
                .Where(n => n.OrganizationId == orgId)
                .ToListAsync(cancellationToken);
            dbContext.Notifications.RemoveRange(notifications);

            // Load and remove notification templates
            var notificationTemplates = await dbContext.NotificationTemplates
                .Where(n => n.OrganizationId == orgId)
                .ToListAsync(cancellationToken);
            dbContext.NotificationTemplates.RemoveRange(notificationTemplates);

            // Load and remove deposit payments
            var depositPayments = await dbContext.DepositPayments
                .Where(d => d.OrganizationId == orgId)
                .ToListAsync(cancellationToken);
            dbContext.DepositPayments.RemoveRange(depositPayments);
            
            // Load and remove rent payments
            var rentPayments = await dbContext.RentPayments
                .Where(r => r.OrganizationId == orgId)
                .ToListAsync(cancellationToken);
            dbContext.RentPayments.RemoveRange(rentPayments);
            
            // Load and remove documents
            var documents = await dbContext.Documents
                .Where(d => d.OrganizationId == orgId)
                .ToListAsync(cancellationToken);
            dbContext.Documents.RemoveRange(documents);
            
            // Load and remove expenses
            var expenses = await dbContext.Expenses
                .Where(e => e.OrganizationId == orgId)
                .ToListAsync(cancellationToken);
            dbContext.Expenses.RemoveRange(expenses);
            
            // Load and remove tenancies
            var tenancies = await dbContext.Tenancies
                .Where(t => t.OrganizationId == orgId)
                .ToListAsync(cancellationToken);
            dbContext.Tenancies.RemoveRange(tenancies);
            
            // Load and remove tenants
            var tenants = await dbContext.Tenants
                .Where(t => t.OrganizationId == orgId)
                .ToListAsync(cancellationToken);
            dbContext.Tenants.RemoveRange(tenants);
            
            // Load and remove houses
            var houses = await dbContext.Houses
                .Where(h => h.OrganizationId == orgId)
                .ToListAsync(cancellationToken);
            dbContext.Houses.RemoveRange(houses);

            // Save all changes
            await dbContext.SaveChangesAsync(cancellationToken);

            var deletedCounts = new Dictionary<string, int>
            {
                ["NotificationAttempts"] = notificationAttempts.Count,
                ["Notifications"] = notifications.Count,
                ["NotificationTemplates"] = notificationTemplates.Count,
                ["DepositPayments"] = depositPayments.Count,
                ["RentPayments"] = rentPayments.Count,
                ["Documents"] = documents.Count,
                ["Expenses"] = expenses.Count,
                ["Tenancies"] = tenancies.Count,
                ["Tenants"] = tenants.Count,
                ["Houses"] = houses.Count
            };

            _logger.LogInformation("Cleared data for organization {OrgId}: {@DeletedCounts}", orgId, deletedCounts);

            return Ok(new 
            { 
                success = true, 
                message = "All data cleared successfully", 
                deletedCounts 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Clear data failed");
            return StatusCode(500, new { error = "Clear data failed", message = ex.Message });
        }
    }
}
