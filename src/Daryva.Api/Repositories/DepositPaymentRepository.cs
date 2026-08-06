using Daryva.Api.Data;
using Daryva.Api.Domain;
using Daryva.Api.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Daryva.Api.Repositories;

public class DepositPaymentRepository : OrgScopedRepository<DepositPayment>, IDepositPaymentRepository
{
    public DepositPaymentRepository(AppDbContext dbContext) : base(dbContext)
    {
    }

    public Task<Dictionary<Guid, decimal>> GetTotalsByTenancyIdAsync(IReadOnlyCollection<Guid> tenancyIds, CancellationToken cancellationToken = default)
        => Set.AsNoTracking()
            .Where(p => tenancyIds.Contains(p.TenancyId) && !p.IsVoided)
            .GroupBy(p => p.TenancyId)
            .Select(g => new { g.Key, Amount = g.Sum(x => x.AmountPaid) })
            .ToDictionaryAsync(x => x.Key, x => x.Amount, cancellationToken);
}
