using Daryva.Api.Data;
using Daryva.Api.Domain;
using Daryva.Api.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Daryva.Api.Repositories;

public class TenantRepository : OrgScopedRepository<Tenant>, ITenantRepository
{
    public TenantRepository(AppDbContext dbContext) : base(dbContext)
    {
    }

    public Task<List<Tenant>> GetAllAsync(bool includeArchived, CancellationToken cancellationToken = default)
    {
        var query = Set.Include(t => t.Tenancies).ThenInclude(te => te.House).AsNoTracking().AsQueryable();
        if (!includeArchived)
            query = query.Where(t => !t.IsArchived);
        return query.ToListAsync(cancellationToken);
    }

    public Task<Tenant?> GetByIdAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => Set.Include(t => t.Tenancies).ThenInclude(te => te.House).AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);

    public Task<Tenant?> GetTrackedByIdAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => Set.FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);

    public Task<Tenant?> GetTrackedWithTenanciesByIdAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => Set.Include(t => t.Tenancies).FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);

    public Task<List<Tenant>> GetActiveAsync(IReadOnlyCollection<Guid>? tenantIds, CancellationToken cancellationToken = default)
    {
        var query = Set.AsNoTracking().Where(t => !t.IsArchived);
        if (tenantIds != null)
            query = query.Where(t => tenantIds.Contains(t.Id));
        return query.ToListAsync(cancellationToken);
    }

    public Task<List<Tenant>> GetAllByAppUserIdAsync(Guid appUserId, CancellationToken cancellationToken = default)
        => Set.IgnoreQueryFilters().AsNoTracking()
            .Where(t => t.AppUserId == appUserId && !t.IsArchived)
            .ToListAsync(cancellationToken);

    public Task<Tenant?> GetByInviteTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default)
        => Set.IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.InviteTokenHash == tokenHash, cancellationToken);
}
