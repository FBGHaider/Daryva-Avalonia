using Daryva.Api.Data;
using Daryva.Api.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Daryva.Api.Repositories;

/// <summary>
/// Base for repositories over org-scoped entities (future domains: Houses, Tenants, Payments, etc).
/// Tenant isolation is already enforced by AppDbContext's global query filters (HasQueryFilter on
/// OrganizationId), so any query through <see cref="Set"/> is automatically scoped -- this base does
/// not re-filter. It only exists to keep DbContext out of services and give each concrete repository
/// a place to add its own intention-revealing methods.
/// </summary>
public abstract class OrgScopedRepository<TEntity> where TEntity : class, IOrgScopedEntity
{
    protected readonly AppDbContext DbContext;

    protected OrgScopedRepository(AppDbContext dbContext)
    {
        DbContext = dbContext;
    }

    protected DbSet<TEntity> Set => DbContext.Set<TEntity>();

    public virtual void Add(TEntity entity) => Set.Add(entity);

    public virtual void Remove(TEntity entity) => Set.Remove(entity);
}
