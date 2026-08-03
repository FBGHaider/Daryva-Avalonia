using Daryva.Api.Data;
using Daryva.Api.Dtos;
using Daryva.Api.Security.Interfaces;
using Daryva.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Daryva.Api.Services;

/// <summary>
/// Implementation of IRentLedgerService.
/// </summary>
public class RentLedgerService : IRentLedgerService
{
    private readonly AppDbContext _dbContext;
    private readonly ITenantContext _tenantContext;

    public RentLedgerService(AppDbContext dbContext, ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    public async Task<IReadOnlyList<RentLedgerItemResponse>> GetRentLedgerEntriesAsync(
        int year,
        int month,
        Guid? houseId = null,
        string? statusFilter = null,
        string? searchTerm = null,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.CurrentOrgId.HasValue)
            return Array.Empty<RentLedgerItemResponse>();

        var orgId = _tenantContext.CurrentOrgId.Value;

        var periodStart = DateTime.SpecifyKind(new DateTime(year, month, 1), DateTimeKind.Utc);
        var periodEnd = DateTime.SpecifyKind(new DateTime(year, month, DateTime.DaysInMonth(year, month)), DateTimeKind.Utc);
        var periodEndExclusive = periodStart.AddMonths(1);

        // Include payments whose date (year/month) falls in the selected month so ledger stays in sync with transactions.
        // Using calendar month avoids timezone/end-of-day excluding payments that transactions show for "this month".
        var tenanciesQuery = _dbContext.Tenancies
            .AsNoTracking()
            .Include(t => t.Tenant)
            .Include(t => t.House)
            .Where(t => t.OrganizationId == orgId)
            .Where(t => !t.Tenant.IsArchived)
            .Where(t => t.MoveInDate <= periodEnd && (!t.MoveOutDate.HasValue || t.MoveOutDate.Value >= periodStart));

        if (houseId.HasValue)
            tenanciesQuery = tenanciesQuery.Where(t => t.HouseId == houseId.Value);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var search = searchTerm.Trim().ToLower();
            tenanciesQuery = tenanciesQuery.Where(t =>
                t.Tenant.FullName.ToLower().Contains(search) ||
                t.House.AddressLine1.ToLower().Contains(search));
        }

        var tenancies = await tenanciesQuery.ToListAsync(cancellationToken);
        var tenancyIds = tenancies.Select(t => t.Id).Distinct().ToList();

        // Payments in this calendar month: use UTC range so any payment recorded in the month is included
        // (avoids timezone shifts where DatePaid.Year/Month could be the previous month).
        var periodStartUtc = DateTime.SpecifyKind(new DateTime(year, month, 1, 0, 0, 0), DateTimeKind.Utc);
        var periodEndExclusiveUtc = periodStartUtc.AddMonths(1);
        var periodRentPayments = await _dbContext.RentPayments
            .AsNoTracking()
            .Where(p => p.OrganizationId == orgId &&
                        tenancyIds.Contains(p.TenancyId) &&
                        p.DatePaid >= periodStartUtc &&
                        p.DatePaid < periodEndExclusiveUtc &&
                        !p.IsVoided)
            .OrderByDescending(p => p.DatePaid)
            .ToListAsync(cancellationToken);

        var paidByTenancy = periodRentPayments
            .GroupBy(p => p.TenancyId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.AmountPaid));

        // Group by (TenantId, HouseId) so we never miss payments due to name/address string differences.
        var tenancyGroups = tenancies
            .GroupBy(t => new { t.TenantId, t.HouseId })
            .ToList();

        var dedupedTenancies = tenancyGroups
            .Select(group =>
            {
                var groupTenancyIds = group.Select(t => t.Id).ToHashSet();
                var groupPaid = periodRentPayments.Where(p => groupTenancyIds.Contains(p.TenancyId)).Sum(p => p.AmountPaid);
                return group
                    .OrderByDescending(t => paidByTenancy.TryGetValue(t.Id, out var paid) ? paid : 0m)
                    .ThenByDescending(t => t.MoveInDate)
                    .ThenByDescending(t => t.RentStartYear ?? t.MoveInDate.Year)
                    .ThenByDescending(t => t.RentStartMonth ?? t.MoveInDate.Month)
                    .First();
            })
            .ToList();

        // For each (tenant, house) group, all tenancy IDs so we sum payments across the group.
        var tenancyIdToGroupIds = tenancyGroups
            .SelectMany(g => g.Select(t => (t.Id, GroupIds: g.Select(x => x.Id).ToHashSet())))
            .ToDictionary(x => x.Id, x => x.GroupIds);

        var totalDepositByTenancy = await _dbContext.DepositPayments
            .AsNoTracking()
            .Where(p => p.OrganizationId == orgId && tenancyIds.Contains(p.TenancyId) && !p.IsVoided)
            .GroupBy(p => p.TenancyId)
            .Select(g => new { g.Key, Amount = g.Sum(x => x.AmountPaid) })
            .ToDictionaryAsync(x => x.Key, x => x.Amount, cancellationToken);

        var result = new List<RentLedgerItemResponse>();
        foreach (var tenancy in dedupedTenancies)
        {
            if (tenancy.Tenant == null || tenancy.House == null)
                continue;

            var rentStartYear = tenancy.RentStartYear ?? tenancy.MoveInDate.Year;
            var rentStartMonth = tenancy.RentStartMonth ?? tenancy.MoveInDate.Month;
            var selectedPeriodNum = year * 12 + month;
            var firstRentPeriodNum = rentStartYear * 12 + rentStartMonth;
            if (selectedPeriodNum < firstRentPeriodNum)
                continue;

            // Sum payments for this logical (tenant, house): include all tenancy IDs in the same group so ledger matches transactions.
            var groupIds = tenancyIdToGroupIds.TryGetValue(tenancy.Id, out var ids) ? ids : new HashSet<Guid> { tenancy.Id };
            var paymentsForPeriod = periodRentPayments.Where(p => groupIds.Contains(p.TenancyId)).ToList();
            var amountPaid = paymentsForPeriod.Sum(p => p.AmountPaid);
            var amountDue = tenancy.RentAmountMonthly;
            var dueDay = Math.Min(Math.Max((int)tenancy.PaymentDueDay, 1), 28);
            var dueDate = new DateTime(year, month, dueDay);

            if (tenancy.MoveOutDate.HasValue && tenancy.MoveOutDate.Value.Date <= dueDate.Date && amountPaid <= 0)
                continue;

            var status = amountPaid >= amountDue
                ? "Paid"
                : amountPaid > 0
                    ? "PartPaid"
                    : dueDate < DateTime.UtcNow.Date
                        ? "Overdue"
                        : "Unpaid";

            if (!MatchesStatusFilter(statusFilter, status))
                continue;

            totalDepositByTenancy.TryGetValue(tenancy.Id, out var totalDepositPaid);
            var depositRemaining = Math.Max(0m, tenancy.DepositAmount - totalDepositPaid);

            result.Add(new RentLedgerItemResponse
            {
                TenancyId = tenancy.Id,
                TenantId = tenancy.TenantId,
                HouseId = tenancy.HouseId,
                HouseAddress = $"{tenancy.House.AddressLine1}, {tenancy.House.City}",
                TenantName = tenancy.Tenant.FullName,
                DueDate = dueDate,
                AmountDue = amountDue,
                AmountPaid = amountPaid,
                Status = status,
                DepositRemaining = depositRemaining,
                PaymentsForThisMonth = paymentsForPeriod.Select(p => new PaymentDetailApiResponse
                {
                    PaymentId = p.Id,
                    PaidOn = p.DatePaid,
                    Amount = p.AmountPaid,
                    Method = p.PaymentMethod,
                    Reference = p.ReferenceNumber,
                    Notes = p.Notes,
                    CollectedBy = p.CollectedBy
                }).ToList()
            });
        }

        return result.OrderBy(r => r.HouseAddress).ThenBy(r => r.TenantName).ToList();
    }

    public async Task<IReadOnlyList<Guid>?> GetTenancyIdsInSameGroupAsync(Guid tenancyId, CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.CurrentOrgId.HasValue)
            return null;

        var orgId = _tenantContext.CurrentOrgId.Value;
        var tenancy = await _dbContext.Tenancies
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenancyId && t.OrganizationId == orgId, cancellationToken);
        if (tenancy == null)
            return null;

        var ids = await _dbContext.Tenancies
            .AsNoTracking()
            .Where(t => t.OrganizationId == orgId && t.TenantId == tenancy.TenantId && t.HouseId == tenancy.HouseId)
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);
        return ids;
    }

    private static bool MatchesStatusFilter(string? statusFilter, string status)
    {
        if (string.IsNullOrWhiteSpace(statusFilter) || statusFilter == "All")
            return true;
        var normalizedFilter = statusFilter.Replace("-", string.Empty);
        var normalizedStatus = status.Replace("-", string.Empty);
        return normalizedFilter.Equals(normalizedStatus, StringComparison.OrdinalIgnoreCase);
    }
}
