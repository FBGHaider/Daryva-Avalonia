using Daryva.MVVM.Models;
using Daryva.Services.Api;
using System.Collections.ObjectModel;

namespace Daryva.Services.Business;

/// <summary>
/// Incremental migration adapter for payments.
/// Uses API for core payment operations when local tenancy ID can be resolved to API tenancy GUID,
/// otherwise falls back to legacy local PaymentService.
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
        {
            await _legacyPaymentService.RecordPaymentAsync(tenancyId, depositAmount, rentAmount, rentYear, rentMonth, paymentDate, method, reference, notes, collectedBy, useDepositForRent);
            return;
        }

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
        => _legacyPaymentService.GetPaymentsForRentChargeAsync(rentChargeId);

    public Task<decimal> GetTotalUnpaidBalanceForMonthAsync(int year, int month)
        => _legacyPaymentService.GetTotalUnpaidBalanceForMonthAsync(year, month);

    public Task<IEnumerable<DashboardOverdueRentItem>> GetOverdueRentAsync()
        => _legacyPaymentService.GetOverdueRentAsync();

    public Task<IEnumerable<DepositReturnReminderItem>> GetDepositReturnRemindersAsync()
        => _legacyPaymentService.GetDepositReturnRemindersAsync();

    public async Task<bool> UnrecordPaymentAsync(int paymentId, string paymentType)
    {
        var apiPaymentId = _paymentApiService.ResolveApiPaymentId(paymentId, paymentType);
        if (!apiPaymentId.HasValue)
            return await _legacyPaymentService.UnrecordPaymentAsync(paymentId, paymentType);

        try
        {
            return await _paymentApiService.UnrecordPaymentAsync(apiPaymentId.Value, paymentType);
        }
        catch
        {
            return await _legacyPaymentService.UnrecordPaymentAsync(paymentId, paymentType);
        }
    }

    public Task<bool> DeleteAllTransactionsAsync()
        => _legacyPaymentService.DeleteAllTransactionsAsync();

    public Task<int> CleanupDuplicateRentChargesAsync()
        => _legacyPaymentService.CleanupDuplicateRentChargesAsync();

    public Task<int> RepairRentPaymentChargeLinksAsync()
    {
        try
        {
            return _legacyPaymentService.RepairRentPaymentChargeLinksAsync();
        }
        catch
        {
            return Task.FromResult(0);
        }
    }

    public Task RecordDepositReturnedAsync(int tenancyId, DateTime returnedDate, decimal amountReturned, string? notes = null)
        => _legacyPaymentService.RecordDepositReturnedAsync(tenancyId, returnedDate, amountReturned, notes);

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
