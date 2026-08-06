using System.Globalization;
using Daryva.Api.Domain;
using Daryva.Api.Dtos;
using Daryva.Api.Repositories.Interfaces;
using Daryva.Api.Security.Interfaces;
using Daryva.Api.Services.Interfaces;

namespace Daryva.Api.Services;

public class PaymentService : IPaymentService
{
    private readonly ITenancyRepository _tenancyRepository;
    private readonly IRentPaymentRepository _rentPaymentRepository;
    private readonly IDepositPaymentRepository _depositPaymentRepository;
    private readonly IDepositReturnRepository _depositReturnRepository;
    private readonly IRentLedgerService _rentLedgerService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITenantContext _tenantContext;
    private readonly IAuditLogger _auditLogger;

    public PaymentService(
        ITenancyRepository tenancyRepository,
        IRentPaymentRepository rentPaymentRepository,
        IDepositPaymentRepository depositPaymentRepository,
        IDepositReturnRepository depositReturnRepository,
        IRentLedgerService rentLedgerService,
        IUnitOfWork unitOfWork,
        ITenantContext tenantContext,
        IAuditLogger auditLogger)
    {
        _tenancyRepository = tenancyRepository;
        _rentPaymentRepository = rentPaymentRepository;
        _depositPaymentRepository = depositPaymentRepository;
        _depositReturnRepository = depositReturnRepository;
        _rentLedgerService = rentLedgerService;
        _unitOfWork = unitOfWork;
        _tenantContext = tenantContext;
        _auditLogger = auditLogger;
    }

    public async Task<RecordPaymentResponse> RecordPaymentAsync(RecordPaymentRequest request, CancellationToken cancellationToken = default)
    {
        var orgId = _tenantContext.CurrentOrgId!.Value;

        var tenancy = await _tenancyRepository.GetByIdAsync(request.TenancyId, cancellationToken);
        if (tenancy == null)
            throw new KeyNotFoundException("Tenancy not found.");

        // Sanity ceiling, not a real business limit -- catches a fat-fingered extra zero (e.g.
        // 5000000 instead of 500) with no server-side pushback otherwise possible today.
        const decimal maxSanePaymentAmount = 1_000_000m;
        if (request.RentAmount > maxSanePaymentAmount || request.DepositAmount > maxSanePaymentAmount)
            throw new ArgumentException($"Payment amount looks too large (over £{maxSanePaymentAmount:N0}) -- check for a typo.");

        // Store the calendar date (year/month/day) the user selected at midnight UTC so the rent
        // ledger and transactions always show the payment in the same month (no timezone shift).
        var paymentDate = DateTime.SpecifyKind(
            new DateTime(request.PaymentDate.Year, request.PaymentDate.Month, request.PaymentDate.Day, 0, 0, 0),
            DateTimeKind.Utc);

        var response = new RecordPaymentResponse();

        if (!request.UseDepositForRent && request.DepositAmount > 0)
        {
            var depositPayment = new DepositPayment
            {
                Id = Guid.NewGuid(),
                OrganizationId = orgId,
                TenancyId = tenancy.Id,
                DatePaid = paymentDate,
                AmountPaid = request.DepositAmount,
                PaymentMethod = request.PaymentMethod ?? string.Empty,
                ProtectionReference = request.Reference,
                Notes = request.Notes
            };

            _depositPaymentRepository.Add(depositPayment);
            response.DepositPaymentId = depositPayment.Id;
        }

        if (request.RentAmount > 0)
        {
            var rentPayment = new RentPayment
            {
                Id = Guid.NewGuid(),
                OrganizationId = orgId,
                TenancyId = tenancy.Id,
                DatePaid = paymentDate,
                AmountPaid = request.RentAmount,
                PaymentMethod = request.PaymentMethod ?? string.Empty,
                ReferenceNumber = request.Reference,
                Notes = request.Notes,
                CollectedBy = request.CollectedBy,
                PaidFromDeposit = request.UseDepositForRent
            };

            _rentPaymentRepository.Add(rentPayment);
            response.RentPaymentId = rentPayment.Id;
        }

        LogAudit(AuditEventTypes.PaymentRecorded, orgId, nameof(Tenancy), tenancy.Id.ToString(),
            metadata: new { response.DepositPaymentId, response.RentPaymentId, request.DepositAmount, request.RentAmount });
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        response.Success = true;
        return response;
    }

    /// <summary>
    /// Deposit actually still available for this tenancy group -- raw non-voided deposit payments,
    /// minus whatever's already been drawn down via UseDepositForRent (RentPayment.PaidFromDeposit).
    /// Every "how much deposit is left" figure (record-payment dialog, deposit-return reminders)
    /// must go through this, not a raw DepositPayments sum, or the same deposit can be applied to
    /// rent indefinitely and still show as fully available/refundable at move-out.
    /// </summary>
    private async Task<decimal> GetAvailableDepositAsync(IReadOnlyCollection<Guid> tenancyIds, CancellationToken cancellationToken)
    {
        var totalPaid = (await _depositPaymentRepository.GetTotalsByTenancyIdAsync(tenancyIds, cancellationToken)).Values.Sum();
        var usedForRent = (await _rentPaymentRepository.GetTotalsPaidFromDepositByTenancyIdAsync(tenancyIds, cancellationToken)).Values.Sum();
        return Math.Max(0m, totalPaid - usedForRent);
    }

    public async Task<decimal?> GetTotalDepositPaidAsync(Guid tenancyId, CancellationToken cancellationToken = default)
    {
        // Sum deposit across same (tenant, house) group so record-payment dialog shows correct remaining
        var groupIds = await _rentLedgerService.GetTenancyIdsInSameGroupAsync(tenancyId, cancellationToken);
        if (groupIds == null || groupIds.Count == 0)
            return null;

        return await GetAvailableDepositAsync(groupIds, cancellationToken);
    }

    public async Task<decimal?> GetTotalRentPaidForPeriodAsync(Guid tenancyId, int year, int month, CancellationToken cancellationToken = default)
    {
        // Use same (tenant, house) group and same month range as rent ledger
        var groupIds = await _rentLedgerService.GetTenancyIdsInSameGroupAsync(tenancyId, cancellationToken);
        if (groupIds == null || groupIds.Count == 0)
            return null;

        var periodStartUtc = DateTime.SpecifyKind(new DateTime(year, month, 1, 0, 0, 0), DateTimeKind.Utc);
        var periodEndExclusiveUtc = periodStartUtc.AddMonths(1);
        var payments = await _rentPaymentRepository.GetForPeriodAsync(groupIds, periodStartUtc, periodEndExclusiveUtc, cancellationToken);
        return payments.Sum(p => p.AmountPaid);
    }

    public async Task<string?> GetDepositStatusAsync(Guid tenancyId, decimal? requiredAmount, CancellationToken cancellationToken = default)
    {
        var tenancy = await _tenancyRepository.GetByIdAsync(tenancyId, cancellationToken);
        if (tenancy == null)
            return null;

        var target = requiredAmount ?? tenancy.DepositAmount;
        var totals = await _depositPaymentRepository.GetTotalsByTenancyIdAsync(new[] { tenancyId }, cancellationToken);
        var total = totals.GetValueOrDefault(tenancyId, 0m);

        return total >= target
            ? "Paid"
            : total > 0
                ? "PartPaid"
                : "Unpaid";
    }

    public async Task<string?> GetRentStatusForPeriodAsync(Guid tenancyId, int year, int month, CancellationToken cancellationToken = default)
    {
        var tenancy = await _tenancyRepository.GetByIdAsync(tenancyId, cancellationToken);
        if (tenancy == null)
            return null;

        // Use same (tenant, house) group and same month range as rent ledger
        var groupIds = await _rentLedgerService.GetTenancyIdsInSameGroupAsync(tenancyId, cancellationToken);
        var paid = 0m;
        if (groupIds != null && groupIds.Count > 0)
        {
            var periodStartUtc = DateTime.SpecifyKind(new DateTime(year, month, 1, 0, 0, 0), DateTimeKind.Utc);
            var periodEndExclusiveUtc = periodStartUtc.AddMonths(1);
            var payments = await _rentPaymentRepository.GetForPeriodAsync(groupIds, periodStartUtc, periodEndExclusiveUtc, cancellationToken);
            paid = payments.Sum(p => p.AmountPaid);
        }

        var due = tenancy.RentAmountMonthly;
        return paid >= due
            ? "Paid"
            : paid > 0
                ? "PartPaid"
                : "Unpaid";
    }

    public async Task<IEnumerable<RentLedgerItemResponse>> GetRentLedgerAsync(int year, int month, Guid? houseId, string? statusFilter, string? searchTerm, CancellationToken cancellationToken = default)
        => await _rentLedgerService.GetRentLedgerEntriesAsync(year, month, houseId, statusFilter, searchTerm, cancellationToken);

    public async Task<IEnumerable<DepositLedgerItemResponse>> GetDepositLedgerAsync(int year, int month, Guid? houseId, string? statusFilter, string? searchTerm, CancellationToken cancellationToken = default)
    {
        var periodEnd = DateTime.SpecifyKind(new DateTime(year, month, DateTime.DaysInMonth(year, month)), DateTimeKind.Utc);
        var tenancies = await _tenancyRepository.GetForDepositLedgerAsync(periodEnd, houseId, searchTerm, cancellationToken);
        var tenancyIds = tenancies.Select(t => t.Id).Distinct().ToList();

        var depositPayments = await _depositPaymentRepository.GetByTenancyIdsAsync(tenancyIds, cancellationToken);

        var paidDepositByTenancy = depositPayments
            .GroupBy(p => p.TenancyId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.AmountPaid));

        var dedupedTenancies = tenancies
            .GroupBy(t => new
            {
                TenantKey = (t.Tenant != null ? t.Tenant.FullName : string.Empty).Trim().ToLower(),
                HouseKey = $"{(t.House != null ? t.House.AddressLine1 : string.Empty).Trim().ToLower()}|{(t.House != null ? t.House.City : string.Empty).Trim().ToLower()}"
            })
            .Select(group => group
                .OrderByDescending(t => paidDepositByTenancy.TryGetValue(t.Id, out var paid) ? paid : 0m)
                .ThenByDescending(t => t.MoveInDate)
                .ThenByDescending(t => t.RentStartYear ?? t.MoveInDate.Year)
                .ThenByDescending(t => t.RentStartMonth ?? t.MoveInDate.Month)
                .First())
            .ToList();

        var result = new List<DepositLedgerItemResponse>();
        foreach (var tenancy in dedupedTenancies)
        {
            if (tenancy.Tenant == null || tenancy.House == null)
            {
                continue;
            }

            var payments = depositPayments.Where(p => p.TenancyId == tenancy.Id).ToList();
            var amountPaid = payments.Sum(p => p.AmountPaid);
            var required = tenancy.DepositAmount;
            var status = amountPaid >= required
                ? "Paid"
                : amountPaid > 0
                    ? "PartPaid"
                    : "Unpaid";

            if (!MatchesStatusFilter(statusFilter, status))
            {
                continue;
            }

            result.Add(new DepositLedgerItemResponse
            {
                TenancyId = tenancy.Id,
                TenantId = tenancy.TenantId,
                HouseId = tenancy.HouseId,
                HouseAddress = $"{tenancy.House.AddressLine1}, {tenancy.House.City}",
                TenantName = tenancy.Tenant.FullName,
                DepositRequired = required,
                AmountPaid = amountPaid,
                Status = status,
                Payments = payments.Select(p => new PaymentDetailApiResponse
                {
                    PaymentId = p.Id,
                    PaidOn = p.DatePaid,
                    Amount = p.AmountPaid,
                    Method = p.PaymentMethod,
                    Reference = p.ProtectionReference,
                    Notes = p.Notes,
                    CollectedBy = null
                }).ToList()
            });
        }

        return result.OrderBy(r => r.HouseAddress).ThenBy(r => r.TenantName);
    }

    public async Task<IEnumerable<TransactionItemResponse>> GetTransactionsAsync(
        DateTime? startDate,
        DateTime? endDate,
        string? paymentType,
        Guid? houseId,
        Guid? tenantId,
        string? method,
        CancellationToken cancellationToken = default)
    {
        var includeRent = string.IsNullOrWhiteSpace(paymentType) || paymentType == "All" || paymentType == "Rent";
        var includeDeposit = string.IsNullOrWhiteSpace(paymentType) || paymentType == "All" || paymentType == "Deposit";
        var normalizedMethod = NormalizeMethod(method);
        // SpecifyKind, not ToUniversalTime(): model-bound DateTime? from a plain date query string
        // (no timezone offset) comes in as DateTimeKind.Unspecified, and ToUniversalTime() on an
        // Unspecified value assumes it's in the SERVER's local timezone and shifts it -- silently
        // moving the requested date range depending on host TZ, unlike every other date computation
        // in this file/RentLedgerService which deliberately avoids this via SpecifyKind(..., Utc).
        var startDateUtc = startDate.HasValue ? DateTime.SpecifyKind(startDate.Value, DateTimeKind.Utc) : (DateTime?)null;
        var endDateUtc = endDate.HasValue ? DateTime.SpecifyKind(endDate.Value, DateTimeKind.Utc) : (DateTime?)null;
        var endDateIsDateOnly = endDate.HasValue && endDate.Value.TimeOfDay == TimeSpan.Zero;
        var endDateExclusiveUtc = endDateIsDateOnly && endDateUtc.HasValue
            ? DateTime.SpecifyKind(endDateUtc.Value.Date.AddDays(1), DateTimeKind.Utc)
            : endDateUtc;
        var transactions = new List<TransactionItemResponse>();

        if (includeRent)
        {
            var rentPayments = await _rentPaymentRepository.QueryTransactionsAsync(
                startDateUtc, endDateExclusiveUtc, endDateIsDateOnly, houseId, tenantId, normalizedMethod, cancellationToken);

            transactions.AddRange(rentPayments.Select(p => new TransactionItemResponse
            {
                PaymentId = p.Id,
                PaymentType = "Rent",
                PaidOn = p.DatePaid,
                TenantName = p.Tenancy.Tenant.FullName,
                HouseAddress = $"{p.Tenancy.House.AddressLine1}, {p.Tenancy.House.City}",
                PeriodLabel = new DateTime(p.DatePaid.Year, p.DatePaid.Month, 1).ToString("MMM yyyy", CultureInfo.InvariantCulture),
                Amount = p.AmountPaid,
                Method = p.PaymentMethod,
                Reference = p.ReferenceNumber,
                Notes = p.Notes,
                CollectedBy = p.CollectedBy,
                TenancyId = p.TenancyId
            }));
        }

        if (includeDeposit)
        {
            var depositPayments = await _depositPaymentRepository.QueryTransactionsAsync(
                startDateUtc, endDateExclusiveUtc, endDateIsDateOnly, houseId, tenantId, normalizedMethod, cancellationToken);

            transactions.AddRange(depositPayments.Select(p => new TransactionItemResponse
            {
                PaymentId = p.Id,
                PaymentType = "Deposit",
                PaidOn = p.DatePaid,
                TenantName = p.Tenancy.Tenant.FullName,
                HouseAddress = $"{p.Tenancy.House.AddressLine1}, {p.Tenancy.House.City}",
                PeriodLabel = string.Empty,
                Amount = p.AmountPaid,
                Method = p.PaymentMethod,
                Reference = p.ProtectionReference,
                Notes = p.Notes,
                CollectedBy = null,
                TenancyId = p.TenancyId
            }));
        }

        return transactions.OrderByDescending(t => t.PaidOn);
    }

    public async Task<IEnumerable<DepositReturnReminderResponse>> GetDepositReturnRemindersAsync(CancellationToken cancellationToken = default)
    {
        var endedTenancyIdsWithReturn = await _depositReturnRepository.GetTenancyIdsWithReturnAsync(cancellationToken);

        // Only show tenancies that have actually ended with a real leave date (exclude default/sentinel like 01/01/0001)
        var minValidLeaveYear = 2000;
        var endedTenancies = await _tenancyRepository.GetEndedWithDepositExcludingAsync(endedTenancyIdsWithReturn, minValidLeaveYear, cancellationToken);

        var tenancyIds = endedTenancies.Select(t => t.Id).ToList();
        // Raw deposit paid, minus whatever's already been drawn down via UseDepositForRent -- see
        // GetAvailableDepositAsync's doc comment. Without this, a deposit spent on rent mid-tenancy
        // still shows as fully owed back to the tenant at move-out.
        var depositTotals = await _depositPaymentRepository.GetTotalsByTenancyIdAsync(tenancyIds, cancellationToken);
        var depositUsedForRent = await _rentPaymentRepository.GetTotalsPaidFromDepositByTenancyIdAsync(tenancyIds, cancellationToken);

        var availableDeposit = tenancyIds.ToDictionary(
            id => id,
            id => Math.Max(0m,
                depositTotals.GetValueOrDefault(id, 0m) - depositUsedForRent.GetValueOrDefault(id, 0m)));

        return endedTenancies
            .Where(t => t.Tenant != null && t.House != null && availableDeposit.TryGetValue(t.Id, out var total) && total > 0)
            .Select(t => new DepositReturnReminderResponse
            {
                TenancyId = t.Id,
                TenantName = t.Tenant!.FullName,
                HouseAddress = $"{t.House!.AddressLine1}, {t.House.City}",
                LeaveDate = t.MoveOutDate!.Value,
                AmountToReturn = availableDeposit[t.Id]
            })
            .OrderBy(r => r.LeaveDate)
            .ToList();
    }

    public async Task RecordDepositReturnedAsync(RecordDepositReturnedRequest request, CancellationToken cancellationToken = default)
    {
        var orgId = _tenantContext.CurrentOrgId!.Value;

        if (request.AmountReturned < 0)
            throw new ArgumentException("Amount returned cannot be negative.");

        if (request.ReturnedDate.Year < 2000)
            throw new ArgumentException("Returned date must be a valid date (year >= 2000).");

        var tenancy = await _tenancyRepository.GetByIdAsync(request.TenancyId, cancellationToken);
        if (tenancy == null)
            throw new KeyNotFoundException("Tenancy not found.");

        var alreadyReturned = await _depositReturnRepository.AnyForTenancyAsync(request.TenancyId, cancellationToken);
        if (alreadyReturned)
            throw new InvalidOperationException("A deposit return has already been recorded for this tenancy.");

        var groupIds = await _rentLedgerService.GetTenancyIdsInSameGroupAsync(request.TenancyId, cancellationToken);
        var availableDeposit = await GetAvailableDepositAsync(groupIds ?? new List<Guid> { request.TenancyId }, cancellationToken);
        if (request.AmountReturned > availableDeposit)
        {
            throw new ArgumentException(
                $"Amount returned (£{request.AmountReturned:0.00}) exceeds the deposit actually available for this tenancy (£{availableDeposit:0.00}).");
        }

        var returnedDateUtc = request.ReturnedDate.Kind == DateTimeKind.Utc
            ? request.ReturnedDate
            : DateTime.SpecifyKind(request.ReturnedDate.Date, DateTimeKind.Utc);

        var depositReturn = new DepositReturn
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            TenancyId = request.TenancyId,
            ReturnedDate = returnedDateUtc,
            AmountReturned = request.AmountReturned,
            Notes = request.Notes
        };

        _depositReturnRepository.Add(depositReturn);
        LogAudit(AuditEventTypes.DepositReturnRecorded, orgId, nameof(Tenancy), tenancy.Id.ToString(),
            metadata: new { depositReturn.AmountReturned });
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    // Voids rather than hard-deletes (task #44): financial records are kept for history, just
    // excluded from ledgers/totals/transactions everywhere those queries filter !IsVoided.
    public async Task VoidAllTransactionsAsync(CancellationToken cancellationToken = default)
    {
        var orgId = _tenantContext.CurrentOrgId!.Value;

        // ExecuteUpdateAsync bypasses the change tracker and commits immediately on its own -- without
        // an explicit transaction wrapping it and the audit entry below, a crash/timeout between the
        // two ExecuteUpdateAsync calls (or between them and the final SaveChangesAsync) would leave an
        // org's entire payment history permanently voided with NO audit record of who did it or when.
        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var rentVoided = await _rentPaymentRepository.VoidAllForOrgAsync(orgId, cancellationToken);
            var depositVoided = await _depositPaymentRepository.VoidAllForOrgAsync(orgId, cancellationToken);

            LogAudit(AuditEventTypes.PaymentsBulkVoided, orgId, nameof(Organization), orgId.ToString(),
                metadata: new { rentPaymentsVoided = rentVoided, depositPaymentsVoided = depositVoided });
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    // Voids rather than hard-deletes (task #44): see VoidAllTransactionsAsync above.
    public async Task<bool> UnrecordPaymentAsync(string paymentType, Guid paymentId, CancellationToken cancellationToken = default)
    {
        var orgId = _tenantContext.CurrentOrgId!.Value;

        if (paymentType.Equals("Rent", StringComparison.OrdinalIgnoreCase))
        {
            var payment = await _rentPaymentRepository.GetTrackedByIdAsync(orgId, paymentId, cancellationToken);
            if (payment == null)
                return false;

            if (payment.IsVoided)
                return true;

            payment.IsVoided = true;
            LogAudit(AuditEventTypes.PaymentVoided, orgId, nameof(RentPayment), payment.Id.ToString(),
                metadata: new { paymentType = "Rent", payment.AmountPaid });
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return true;
        }

        if (paymentType.Equals("Deposit", StringComparison.OrdinalIgnoreCase))
        {
            var payment = await _depositPaymentRepository.GetTrackedByIdAsync(orgId, paymentId, cancellationToken);
            if (payment == null)
                return false;

            if (payment.IsVoided)
                return true;

            payment.IsVoided = true;
            LogAudit(AuditEventTypes.PaymentVoided, orgId, nameof(DepositPayment), payment.Id.ToString(),
                metadata: new { paymentType = "Deposit", payment.AmountPaid });
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return true;
        }

        throw new ArgumentException("paymentType must be 'Rent' or 'Deposit'.");
    }

    private static bool MatchesStatusFilter(string? statusFilter, string status)
    {
        if (string.IsNullOrWhiteSpace(statusFilter) || statusFilter == "All")
        {
            return true;
        }

        var normalizedFilter = statusFilter.Replace("-", string.Empty);
        var normalizedStatus = status.Replace("-", string.Empty);
        return normalizedFilter.Equals(normalizedStatus, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeMethod(string? method)
    {
        return string.IsNullOrWhiteSpace(method)
            ? string.Empty
            : method.Replace(" ", string.Empty).Trim();
    }

    private void LogAudit(string eventType, Guid organizationId, string targetType, string targetId, object? metadata = null)
    {
        if (!Guid.TryParse(_tenantContext.UserId, out var actorId))
            return;

        _auditLogger.Log(actorId, _tenantContext.CurrentRole ?? "Unknown", eventType,
            organizationId: organizationId, targetType: targetType, targetId: targetId, metadata: metadata,
            supportSessionId: _tenantContext.ActiveSupportSessionId);
    }
}
