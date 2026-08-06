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

    public Task<List<DepositPayment>> GetByTenancyIdsAsync(IReadOnlyCollection<Guid> tenancyIds, CancellationToken cancellationToken = default)
        => Set.AsNoTracking()
            .Where(p => tenancyIds.Contains(p.TenancyId) && !p.IsVoided)
            .OrderByDescending(p => p.DatePaid)
            .ToListAsync(cancellationToken);

    public Task<DepositPayment?> GetTrackedByIdAsync(Guid orgId, Guid paymentId, CancellationToken cancellationToken = default)
        => Set.FirstOrDefaultAsync(p => p.Id == paymentId && p.OrganizationId == orgId, cancellationToken);

    public Task<int> VoidAllForOrgAsync(Guid orgId, CancellationToken cancellationToken = default)
        => Set.Where(p => p.OrganizationId == orgId && !p.IsVoided)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.IsVoided, true), cancellationToken);

    public Task<List<DepositPayment>> QueryTransactionsAsync(
        DateTime? startDateUtc,
        DateTime? endDateExclusiveUtc,
        bool endDateIsDateOnly,
        Guid? houseId,
        Guid? tenantId,
        string? normalizedMethod,
        CancellationToken cancellationToken = default)
    {
        var query = Set.AsNoTracking()
            .Include(p => p.Tenancy)
                .ThenInclude(t => t.Tenant)
            .Include(p => p.Tenancy)
                .ThenInclude(t => t.House)
            .Where(p => !p.IsVoided)
            .AsQueryable();

        if (startDateUtc.HasValue) query = query.Where(p => p.DatePaid >= startDateUtc.Value);
        if (endDateExclusiveUtc.HasValue)
        {
            query = endDateIsDateOnly
                ? query.Where(p => p.DatePaid < endDateExclusiveUtc.Value)
                : query.Where(p => p.DatePaid <= endDateExclusiveUtc.Value);
        }
        if (houseId.HasValue) query = query.Where(p => p.Tenancy.HouseId == houseId.Value);
        if (tenantId.HasValue) query = query.Where(p => p.Tenancy.TenantId == tenantId.Value);
        if (!string.IsNullOrWhiteSpace(normalizedMethod))
        {
            // Inlined rather than calling a helper method: EF Core cannot translate an arbitrary
            // C# method call inside a query expression (only the individual string operations it's
            // built from), regardless of which class the method lives on.
            query = query.Where(p => (string.IsNullOrWhiteSpace(p.PaymentMethod) ? string.Empty : p.PaymentMethod.Replace(" ", string.Empty).Trim()) == normalizedMethod);
        }

        return query.ToListAsync(cancellationToken);
    }
}
