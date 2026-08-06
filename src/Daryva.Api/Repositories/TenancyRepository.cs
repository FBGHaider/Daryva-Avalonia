using Daryva.Api.Data;
using Daryva.Api.Domain;
using Daryva.Api.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Daryva.Api.Repositories;

public class TenancyRepository : OrgScopedRepository<Tenancy>, ITenancyRepository
{
    public TenancyRepository(AppDbContext dbContext) : base(dbContext)
    {
    }

    public Task<List<Tenancy>> GetActiveByHouseIdAsync(Guid houseId, CancellationToken cancellationToken = default)
        => Set.AsNoTracking()
            .Where(t => t.HouseId == houseId && t.Status == "Active")
            .ToListAsync(cancellationToken);

    public Task<Tenancy?> GetLatestActiveWithHouseByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => Set.AsNoTracking()
            .Include(t => t.House)
            .Where(t => t.TenantId == tenantId && t.Status == "Active")
            .OrderByDescending(t => t.MoveInDate)
            .FirstOrDefaultAsync(cancellationToken);
}
