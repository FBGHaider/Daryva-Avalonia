using Daryva.Api.Data;
using Daryva.Api.Domain;
using Daryva.Api.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Daryva.Api.Repositories;

public class HouseRepository : OrgScopedRepository<House>, IHouseRepository
{
    public HouseRepository(AppDbContext dbContext) : base(dbContext)
    {
    }

    public Task<List<House>> GetAllAsync(bool includeArchived, CancellationToken cancellationToken = default)
    {
        var query = Set.AsNoTracking().AsQueryable();
        if (!includeArchived)
            query = query.Where(h => !h.IsArchived);
        return query.ToListAsync(cancellationToken);
    }

    public Task<House?> GetByIdAsync(Guid houseId, CancellationToken cancellationToken = default)
        => Set.AsNoTracking().FirstOrDefaultAsync(h => h.Id == houseId, cancellationToken);

    public Task<House?> GetTrackedByIdAsync(Guid houseId, CancellationToken cancellationToken = default)
        => Set.FirstOrDefaultAsync(h => h.Id == houseId, cancellationToken);
}
