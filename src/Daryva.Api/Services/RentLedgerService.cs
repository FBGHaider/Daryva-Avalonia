using Daryva.Api.Data;
using Daryva.Api.Dtos;
using Daryva.Api.Security;
using Microsoft.EntityFrameworkCore;

namespace Daryva.Api.Services;

/// <summary>
/// Single source of truth for rent ledger entries (who has rent due in a period and how much).
/// Used by the Rent & Payments tab and by house stats so house monthly rent matches the ledger.
/// </summary>
public interface IRentLedgerService
{
    /// <summary>
    /// Returns the same entries that appear in the rent ledger for the given period.
    /// Dedupes by tenant+house, respects rent start period and move-out, excludes archived tenants.
    /// </summary>
    Task<IReadOnlyList<RentLedgerItemResponse>> GetRentLedgerEntriesAsync(
        int year,
        int month,
        Guid? houseId = null,
        string? statusFilter = null,
        string? searchTerm = null,
        CancellationToken cancellationToken = default);
}

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

        var periodRentPayments = await _dbContext.RentPayments
            .AsNoTracking()
            .Where(p => p.OrganizationId == orgId && tenancyIds.Contains(p.TenancyId) && p.DatePaid >= periodStart && p.DatePaid < periodEndExclusive)
            .OrderByDescending(p => p.DatePaid)
            .ToListAsync(cancellationToken);

        var paidByTenancy = periodRentPayments
            .GroupBy(p => p.TenancyId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.AmountPaid));

        var dedupedTenancies = tenancies
            .GroupBy(t => new
            {
                TenantKey = (t.Tenant != null ? t.Tenant.FullName : string.Empty).Trim().ToLower(),
                HouseKey = $"{(t.House != null ? t.House.AddressLine1 : string.Empty).Trim().ToLower()}|{(t.House != null ? t.House.City : string.Empty).Trim().ToLower()}"
            })
            .Select(group => group
                .OrderByDescending(t => paidByTenancy.TryGetValue(t.Id, out var paid) ? paid : 0m)
                .ThenByDescending(t => t.MoveInDate)
                .ThenByDescending(t => t.RentStartYear ?? t.MoveInDate.Year)
                .ThenByDescending(t => t.RentStartMonth ?? t.MoveInDate.Month)
                .First())
            .ToList();

        var totalDepositByTenancy = await _dbContext.DepositPayments
            .AsNoTracking()
            .Where(p => p.OrganizationId == orgId && tenancyIds.Contains(p.TenancyId))
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

            var paymentsForPeriod = periodRentPayments.Where(p => p.TenancyId == tenancy.Id).ToList();
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

    private static bool MatchesStatusFilter(string? statusFilter, string status)
    {
        if (string.IsNullOrWhiteSpace(statusFilter) || statusFilter == "All")
            return true;
        var normalizedFilter = statusFilter.Replace("-", string.Empty);
        var normalizedStatus = status.Replace("-", string.Empty);
        return normalizedFilter.Equals(normalizedStatus, StringComparison.OrdinalIgnoreCase);
    }
}
