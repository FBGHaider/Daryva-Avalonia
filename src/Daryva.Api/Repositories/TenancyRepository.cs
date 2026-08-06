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

    public Task<Tenancy?> GetByIdAsync(Guid tenancyId, CancellationToken cancellationToken = default)
        => Set.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tenancyId, cancellationToken);

    public Task<List<Tenancy>> GetForRentLedgerAsync(
        DateTime periodStart,
        DateTime periodEnd,
        Guid? houseId,
        string? searchTerm,
        CancellationToken cancellationToken = default)
    {
        var query = Set.AsNoTracking()
            .Include(t => t.Tenant)
            .Include(t => t.House)
            .Where(t => !t.Tenant.IsArchived)
            .Where(t => t.MoveInDate <= periodEnd && (!t.MoveOutDate.HasValue || t.MoveOutDate.Value >= periodStart));

        if (houseId.HasValue)
            query = query.Where(t => t.HouseId == houseId.Value);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var search = searchTerm.Trim().ToLower();
            query = query.Where(t =>
                t.Tenant.FullName.ToLower().Contains(search) ||
                t.House.AddressLine1.ToLower().Contains(search));
        }

        return query.ToListAsync(cancellationToken);
    }

    public Task<List<Guid>> GetIdsInSameGroupAsync(Guid tenantId, Guid houseId, CancellationToken cancellationToken = default)
        => Set.AsNoTracking()
            .Where(t => t.TenantId == tenantId && t.HouseId == houseId)
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);
}
