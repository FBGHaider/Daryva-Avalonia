using Daryva.Api.Data;
using Daryva.Api.Domain;
using Daryva.Api.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Daryva.Api.Repositories;

public class DepositReturnRepository : OrgScopedRepository<DepositReturn>, IDepositReturnRepository
{
    public DepositReturnRepository(AppDbContext dbContext) : base(dbContext)
    {
    }

    public Task<List<Guid>> GetTenancyIdsWithReturnAsync(CancellationToken cancellationToken = default)
        => Set.AsNoTracking().Select(r => r.TenancyId).Distinct().ToListAsync(cancellationToken);

    public Task<bool> AnyForTenancyAsync(Guid tenancyId, CancellationToken cancellationToken = default)
        => Set.AnyAsync(r => r.TenancyId == tenancyId, cancellationToken);

    public Task<List<DepositReturn>> GetAllAsync(CancellationToken cancellationToken = default)
        => Set.AsNoTracking().ToListAsync(cancellationToken);
}
