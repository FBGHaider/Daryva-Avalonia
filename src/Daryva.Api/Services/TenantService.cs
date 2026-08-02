using Daryva.Api.Data;
using Daryva.Api.Domain;
using Daryva.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Daryva.Api.Services;

/// <summary>
/// Implementation of ITenantService.
/// All queries are automatically filtered by OrgId via EF Core global query filters.
/// </summary>
public class TenantService : ITenantService
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<TenantService> _logger;

    public TenantService(AppDbContext dbContext, ILogger<TenantService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<IEnumerable<Tenant>> GetAllTenantsAsync(
        bool includeArchived = false,
        CancellationToken cancellationToken = default)
    {
        // Global query filter automatically filters by OrganizationId == CurrentOrgId
        var query = _dbContext.Tenants
            .Include(t => t.Tenancies)
                .ThenInclude(te => te.House)
            .AsNoTracking();

        if (!includeArchived)
            query = query.Where(t => !t.IsArchived);

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<Tenant?> GetTenantByIdAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        // Global query filter automatically filters by OrganizationId == CurrentOrgId
        return await _dbContext.Tenants
            .Include(t => t.Tenancies)
                .ThenInclude(te => te.House)
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);
    }

    public async Task<Tenant> CreateTenantAsync(
        Tenant tenant,
        CancellationToken cancellationToken = default)
    {
        _dbContext.Tenants.Add(tenant);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return tenant;
    }

    public async Task UpdateTenantAsync(
        Tenant tenant,
        CancellationToken cancellationToken = default)
    {
        // Load tracked entity only; do not attach the detached graph (Tenancies/House) to avoid 500 on SaveChanges
        var existing = await _dbContext.Tenants
            .FirstOrDefaultAsync(t => t.Id == tenant.Id, cancellationToken);
        if (existing == null)
            return;

        existing.FullName = tenant.FullName;
        existing.Email = tenant.Email ?? string.Empty;
        existing.PhoneNumber = tenant.PhoneNumber ?? string.Empty;
        existing.UniversityName = tenant.UniversityName;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteTenantAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var tenant = await _dbContext.Tenants
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);
        if (tenant != null)
        {
            _dbContext.Tenants.Remove(tenant);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<bool> ArchiveTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var tenant = await _dbContext.Tenants
            .Include(t => t.Tenancies)
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);
        if (tenant == null)
            return false;

        var archiveDate = DateTime.UtcNow.Date;
        foreach (var tenancy in tenant.Tenancies)
        {
            if (string.Equals(tenancy.Status, "Active", StringComparison.OrdinalIgnoreCase)
                && (!tenancy.MoveOutDate.HasValue || tenancy.MoveOutDate.Value.Date > archiveDate))
            {
                tenancy.MoveOutDate = archiveDate;
                tenancy.Status = "Ended";
            }
        }

        tenant.IsArchived = true;
        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Archived tenant {TenantId}", tenantId);
        return true;
    }

    public async Task<bool> UnarchiveTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var tenant = await _dbContext.Tenants
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);
        if (tenant == null)
            return false;

        tenant.IsArchived = false;
        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Unarchived tenant {TenantId}", tenantId);
        return true;
    }
}
