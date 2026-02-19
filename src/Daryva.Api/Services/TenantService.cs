using Daryva.Api.Data;
using Daryva.Api.Domain;
using Daryva.Api.Dtos;
using Daryva.Api.Security;
using Microsoft.EntityFrameworkCore;

namespace Daryva.Api.Services;

/// <summary>
/// Business logic for tenant management.
/// All operations are automatically filtered by current organization via global query filters.
/// </summary>
public interface ITenantService
{
    /// <summary>
    /// Get all tenants for the current organization.
    /// </summary>
    Task<IEnumerable<Tenant>> GetAllTenantsAsync(
        bool includeArchived = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a specific tenant by ID (must belong to current org).
    /// </summary>
    Task<Tenant?> GetTenantByIdAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a new tenant. OrganizationId must be set before calling.
    /// </summary>
    Task<Tenant> CreateTenantAsync(
        Tenant tenant,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Update an existing tenant (if it belongs to current org).
    /// </summary>
    Task UpdateTenantAsync(
        Tenant tenant,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a tenant (if it belongs to current org).
    /// </summary>
    Task DeleteTenantAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);
}

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
        _dbContext.Tenants.Update(tenant);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteTenantAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var tenant = await GetTenantByIdAsync(tenantId, cancellationToken);
        if (tenant != null)
        {
            _dbContext.Tenants.Remove(tenant);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
