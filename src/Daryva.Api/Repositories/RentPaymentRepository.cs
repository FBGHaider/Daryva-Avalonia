using Daryva.Api.Data;
using Daryva.Api.Domain;
using Daryva.Api.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Daryva.Api.Repositories;

public class RentPaymentRepository : OrgScopedRepository<RentPayment>, IRentPaymentRepository
{
    public RentPaymentRepository(AppDbContext dbContext) : base(dbContext)
    {
    }

    public Task<List<RentPayment>> GetForPeriodAsync(
        IReadOnlyCollection<Guid> tenancyIds,
        DateTime periodStartUtc,
        DateTime periodEndExclusiveUtc,
        CancellationToken cancellationToken = default)
        => Set.AsNoTracking()
            .Where(p => tenancyIds.Contains(p.TenancyId) &&
                        p.DatePaid >= periodStartUtc &&
                        p.DatePaid < periodEndExclusiveUtc &&
                        !p.IsVoided)
            .OrderByDescending(p => p.DatePaid)
            .ToListAsync(cancellationToken);
}
