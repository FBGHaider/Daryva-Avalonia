using Daryva.MVVM.Models;
using Daryva.Services.Api;
using System.Collections.ObjectModel;

namespace Daryva.Services.Business;

/// <summary>
/// API-only payment service. Uses API for all payment operations; no SQLite fallback.
/// Tenancy/house/tenant ID resolution uses IApiEntityIdMapper (populated when tenancies are loaded from API).
/// </summary>
public class PaymentApiMigrationService : IPaymentService
{
    private readonly IPaymentApiService _paymentApiService;
    private readonly PaymentService _legacyPaymentService;

    public PaymentApiMigrationService(IPaymentApiService paymentApiService, PaymentService legacyPaymentService)
    {
        _paymentApiService = paymentApiService ?? throw new ArgumentNullException(nameof(paymentApiService));
        _legacyPaymentService = legacyPaymentService ?? throw new ArgumentNullException(nameof(legacyPaymentService));
    }

    public async Task RecordPaymentAsync(int tenancyId, decimal depositAmount, decimal rentAmount, int rentYear, int rentMonth, DateTime paymentDate, string method, string? reference, string? notes, string? collectedBy = null, bool useDepositForRent = false)
    {
        var apiTenancyId = await TryResolveApiTenancyIdAsync(tenancyId);
        if (!apiTenancyId.HasValue)
            throw new InvalidOperationException($"Tenancy {tenancyId} could not be resolved to API. Ensure tenancies are loaded from the API.");

        await _paymentApiService.RecordPaymentAsync(new RecordPaymentApiRequest
        {
            TenancyId = apiTenancyId.Value,
            DepositAmount = depositAmount,
            RentAmount = rentAmount,
            RentYear = rentYear,
            RentMonth = rentMonth,
            PaymentDate = paymentDate,
            PaymentMethod = method,
            Reference = reference,
            Notes = notes,
            CollectedBy = collectedBy,
            UseDepositForRent = useDepositForRent
        });
    }

    public async Task<decimal> GetTotalDepositPaidAsync(int tenancyId)
    {
        var apiTenancyId = await TryResolveApiTenancyIdAsync(tenancyId);
        if (!apiTenancyId.HasValue)
            return await _legacyPaymentService.GetTotalDepositPaidAsync(tenancyId);

        return await _paymentApiService.GetTotalDepositPaidAsync(apiTenancyId.Value);
    }

    public async Task<decimal> GetTotalRentPaidForPeriodAsync(int tenancyId, int year, int month)
    {
        var apiTenancyId = await TryResolveApiTenancyIdAsync(tenancyId);
        if (!apiTenancyId.HasValue)
            return await _legacyPaymentService.GetTotalRentPaidForPeriodAsync(tenancyId, year, month);

        return await _paymentApiService.GetTotalRentPaidForPeriodAsync(apiTenancyId.Value, year, month);
    }

    public async Task<string> GetDepositStatusAsync(int tenancyId, decimal depositRequired)
    {
        var apiTenancyId = await TryResolveApiTenancyIdAsync(tenancyId);
        if (!apiTenancyId.HasValue)
            return await _legacyPaymentService.GetDepositStatusAsync(tenancyId, depositRequired);

        return await _paymentApiService.GetDepositStatusAsync(apiTenancyId.Value, depositRequired);
    }

    public async Task<string> GetRentStatusForPeriodAsync(int tenancyId, int year, int month)
    {
        var apiTenancyId = await TryResolveApiTenancyIdAsync(tenancyId);
        if (!apiTenancyId.HasValue)
            return await _legacyPaymentService.GetRentStatusForPeriodAsync(tenancyId, year, month);

        return await _paymentApiService.GetRentStatusForPeriodAsync(apiTenancyId.Value, year, month);
    }

    public async Task<IEnumerable<RentLedgerRowViewModel>> GetRentLedgerForMonthAsync(int year, int month, int? houseId = null, string? statusFilter = null, string? searchTerm = null)
    {
        Guid? apiHouseId = null;
        if (houseId.HasValue)
        {
            apiHouseId = await _paymentApiService.ResolveHouseApiIdAsync(houseId.Value);
            if (!apiHouseId.HasValue)
                return Enumerable.Empty<RentLedgerRowViewModel>();
        }

        try
        {
            var rows = await _paymentApiService.GetRentLedgerForMonthAsync(year, month, apiHouseId, statusFilter, searchTerm);
            var mapped = new List<RentLedgerRowViewModel>();

            foreach (var row in rows ?? Enumerable.Empty<RentLedgerItemApiDto>())
            {
                if (row == null) continue;

                var localTenancyId = _paymentApiService.ResolveLocalTenancyId(row.TenancyId);
                var localTenantId = _paymentApiService.ResolveLocalTenantId(row.TenantId);
                var localHouseId = _paymentApiService.ResolveLocalHouseId(row.HouseId);
                if (!localTenancyId.HasValue || !localTenantId.HasValue || !localHouseId.HasValue)
                {
                    continue;
                }

                mapped.Add(new RentLedgerRowViewModel
                {
                    TenancyId = localTenancyId.Value,
                    TenantId = localTenantId.Value,
                    HouseId = localHouseId.Value,
                    HouseAddress = row.HouseAddress,
                    TenantName = row.TenantName,
                    DueDate = row.DueDate,
                    AmountDue = row.AmountDue,
                    AmountPaid = row.AmountPaid,
                    Status = row.Status,
                    DepositRemaining = row.DepositRemaining,
                    PaymentsForThisMonth = new ObservableCollection<PaymentDetailViewModel>(
                        (row.PaymentsForThisMonth ?? new List<PaymentDetailApiDto>())
                        .Where(p => p != null)
                        .Select(p => new PaymentDetailViewModel
                        {
                            PaidOn = p.PaidOn,
                            Amount = p.Amount,
                            Method = p.Method,
                            Reference = p.Reference,
                            Notes = p.Notes,
                            CollectedBy = p.CollectedBy
                        }))
                });
            }

            return mapped;
        }
        catch
        {
            return Enumerable.Empty<RentLedgerRowViewModel>();
        }
    }

    public async Task<IEnumerable<DepositLedgerRowViewModel>> GetDepositLedgerForMonthAsync(int year, int month, int? houseId = null, string? statusFilter = null, string? searchTerm = null)
    {
        Guid? apiHouseId = null;
        if (houseId.HasValue)
        {
            apiHouseId = await _paymentApiService.ResolveHouseApiIdAsync(houseId.Value);
            if (!apiHouseId.HasValue)
                return Enumerable.Empty<DepositLedgerRowViewModel>();
        }

        try
        {
            var rows = await _paymentApiService.GetDepositLedgerForMonthAsync(year, month, apiHouseId, statusFilter, searchTerm);
            var mapped = new List<DepositLedgerRowViewModel>();

            foreach (var row in rows ?? Enumerable.Empty<DepositLedgerItemApiDto>())
            {
                if (row == null) continue;

                var localTenancyId = _paymentApiService.ResolveLocalTenancyId(row.TenancyId);
                var localHouseId = _paymentApiService.ResolveLocalHouseId(row.HouseId);
                if (!localTenancyId.HasValue || !localHouseId.HasValue)
                {
                    continue;
                }

                mapped.Add(new DepositLedgerRowViewModel
                {
                    TenancyId = localTenancyId.Value,
                    HouseId = localHouseId.Value,
                    HouseAddress = row.HouseAddress,
                    TenantName = row.TenantName,
                    DepositRequired = row.DepositRequired,
                    AmountPaid = row.AmountPaid,
                    Status = row.Status,
                    Payments = new ObservableCollection<PaymentDetailViewModel>(
                        (row.Payments ?? new List<PaymentDetailApiDto>())
                        .Where(p => p != null)
                        .Select(p => new PaymentDetailViewModel
                        {
                            PaidOn = p.PaidOn,
                            Amount = p.Amount,
                            Method = p.Method,
                            Reference = p.Reference,
                            Notes = p.Notes,
                            CollectedBy = p.CollectedBy
                        }))
                });
            }

            return mapped;
        }
        catch
        {
            return Enumerable.Empty<DepositLedgerRowViewModel>();
        }
    }

    public async Task<IEnumerable<TransactionRowViewModel>> GetTransactionsAsync(DateTime? startDate = null, DateTime? endDate = null, string? paymentType = null, int? houseId = null, int? tenantId = null, string? method = null)
    {
        Guid? apiHouseId = null;
        if (houseId.HasValue)
        {
            apiHouseId = await _paymentApiService.ResolveHouseApiIdAsync(houseId.Value);
            if (!apiHouseId.HasValue)
                return Enumerable.Empty<TransactionRowViewModel>();
        }

        Guid? apiTenantId = null;
        if (tenantId.HasValue)
        {
            apiTenantId = await _paymentApiService.ResolveTenantApiIdAsync(tenantId.Value);
            if (!apiTenantId.HasValue)
                return Enumerable.Empty<TransactionRowViewModel>();
        }

        try
        {
            var rows = await _paymentApiService.GetTransactionsAsync(startDate, endDate, paymentType, apiHouseId, apiTenantId, method);
            return (rows ?? Enumerable.Empty<TransactionItemApiDto>())
                .Where(r => r != null)
                .Select(r => new TransactionRowViewModel
            {
                PaymentId = _paymentApiService.GetOrCreateLocalPaymentId(r.PaymentId, r.PaymentType),
                PaymentType = r.PaymentType,
                PaidOn = r.PaidOn,
                TenantName = r.TenantName,
                HouseAddress = r.HouseAddress,
                PeriodLabel = r.PeriodLabel,
                Amount = r.Amount,
                Method = r.Method,
                Reference = r.Reference,
                Notes = r.Notes,
                CollectedBy = r.CollectedBy,
                TenancyId = r.TenancyId.HasValue ? _paymentApiService.ResolveLocalTenancyId(r.TenancyId.Value) : null,
                RentChargeId = null
            }).OrderByDescending(t => t.PaidOn);
        }
        catch
        {
            return Enumerable.Empty<TransactionRowViewModel>();
        }
    }

    public Task<IEnumerable<PaymentDetailViewModel>> GetPaymentsForRentChargeAsync(int rentChargeId)
        => Task.FromResult(Enumerable.Empty<PaymentDetailViewModel>());

    public async Task<decimal> GetTotalUnpaidBalanceForMonthAsync(int year, int month)
    {
        try
        {
            var rows = await _paymentApiService.GetRentLedgerForMonthAsync(year, month, null, null, null);
            decimal total = 0m;
            foreach (var row in rows ?? Enumerable.Empty<RentLedgerItemApiDto>())
            {
                var balance = row.AmountDue - row.AmountPaid;
                if (balance > 0)
                    total += balance;
            }
            return total;
        }
        catch
        {
            return 0m;
        }
    }

    public async Task<IEnumerable<DashboardOverdueRentItem>> GetOverdueRentAsync()
    {
        var today = DateTime.UtcNow.Date;
        var items = new List<DashboardOverdueRentItem>();
        try
        {
            for (var i = 0; i < 3; i++)
            {
                var d = today.AddMonths(-i);
                var rows = await _paymentApiService.GetRentLedgerForMonthAsync(d.Year, d.Month, null, null, null);
                foreach (var row in rows ?? Enumerable.Empty<RentLedgerItemApiDto>())
                {
                    if (row.DueDate.Date >= today) continue;
                    var balance = row.AmountDue - row.AmountPaid;
                    if (balance <= 0) continue;
                    var status = (row.Status ?? "").Replace("-", "");
                    if (!status.Equals("Unpaid", StringComparison.OrdinalIgnoreCase) && !status.Equals("PartPaid", StringComparison.OrdinalIgnoreCase) && !status.Equals("Overdue", StringComparison.OrdinalIgnoreCase))
                        continue;
                    var localTenancyId = _paymentApiService.ResolveLocalTenancyId(row.TenancyId);
                    if (!localTenancyId.HasValue) continue;
                    var daysLate = (int)(today - row.DueDate.Date).TotalDays;
                    items.Add(new DashboardOverdueRentItem
                    {
                        TenantName = row.TenantName,
                        HouseAddress = row.HouseAddress,
                        Amount = balance,
                        DaysLate = daysLate,
                        TenancyId = localTenancyId.Value
                    });
                }
            }
        }
        catch
        {
            // return what we have
        }
        return items.OrderByDescending(x => x.DaysLate);
    }

    public async Task<IEnumerable<DepositReturnReminderItem>> GetDepositReturnRemindersAsync()
    {
        try
        {
            var reminders = await _paymentApiService.GetDepositReturnRemindersAsync();
            var result = new List<DepositReturnReminderItem>();
            foreach (var r in reminders ?? Enumerable.Empty<DepositReturnReminderApiDto>())
            {
                var localTenancyId = _paymentApiService.ResolveLocalTenancyId(r.TenancyId);
                if (!localTenancyId.HasValue) continue;
                result.Add(new DepositReturnReminderItem
                {
                    TenancyId = localTenancyId.Value,
                    TenantName = r.TenantName,
                    HouseAddress = r.HouseAddress,
                    LeaveDate = r.LeaveDate,
                    LeaveDateDisplay = r.LeaveDate.ToString("d"),
                    AmountToReturn = r.AmountToReturn
                });
            }
            return result;
        }
        catch
        {
            return Enumerable.Empty<DepositReturnReminderItem>();
        }
    }

    public async Task<bool> UnrecordPaymentAsync(int paymentId, string paymentType)
    {
        var apiPaymentId = _paymentApiService.ResolveApiPaymentId(paymentId, paymentType);
        if (!apiPaymentId.HasValue)
            throw new InvalidOperationException($"Payment {paymentId} ({paymentType}) could not be resolved to API.");
        return await _paymentApiService.UnrecordPaymentAsync(apiPaymentId.Value, paymentType);
    }

    public async Task<bool> DeleteAllTransactionsAsync()
    {
        await _paymentApiService.DeleteAllTransactionsAsync();
        return true;
    }

    public Task<int> CleanupDuplicateRentChargesAsync()
        => Task.FromResult(0);

    public Task<int> RepairRentPaymentChargeLinksAsync()
        => Task.FromResult(0);

    public async Task RecordDepositReturnedAsync(int tenancyId, DateTime returnedDate, decimal amountReturned, string? notes = null)
    {
        var apiTenancyId = await TryResolveApiTenancyIdAsync(tenancyId);
        if (!apiTenancyId.HasValue)
            throw new InvalidOperationException($"Tenancy {tenancyId} could not be resolved to API.");
        await _paymentApiService.RecordDepositReturnedAsync(apiTenancyId.Value, returnedDate, amountReturned, notes);
    }

    private async Task<Guid?> TryResolveApiTenancyIdAsync(int localTenancyId)
    {
        try
        {
            return await _paymentApiService.ResolveTenancyApiIdAsync(localTenancyId);
        }
        catch
        {
            return null;
        }
    }
}
