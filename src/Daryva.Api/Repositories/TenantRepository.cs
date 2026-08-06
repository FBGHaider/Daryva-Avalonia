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
}
