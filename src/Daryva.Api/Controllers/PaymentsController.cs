using Daryva.Api.Data;
using Daryva.Api.Domain;
using Daryva.Api.Dtos;
using Daryva.Api.Security;
using Daryva.Api.Security.Interfaces;
using Daryva.Api.Services;
using Daryva.Api.Services.Interfaces;
using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Daryva.Api.Controllers;

[ApiController]
[Route("api/payments")]
[Authorize]
public class PaymentsController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly IRentLedgerService _rentLedgerService;
    private readonly IAuditLogger _auditLogger;

    public PaymentsController(AppDbContext dbContext, ITenantContext tenantContext, IRentLedgerService rentLedgerService, IAuditLogger auditLogger)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _rentLedgerService = rentLedgerService;
        _auditLogger = auditLogger;
    }

    private void LogAudit(string eventType, Guid organizationId, string targetType, string targetId, object? metadata = null)
    {
        if (!Guid.TryParse(_tenantContext.UserId, out var actorId))
            return;

        _auditLogger.Log(actorId, _tenantContext.CurrentRole ?? "Unknown", eventType,
            organizationId: organizationId, targetType: targetType, targetId: targetId, metadata: metadata,
            supportSessionId: _tenantContext.ActiveSupportSessionId);
    }

    [HttpPost("record")]
    [Authorize(Policy = Permissions.Payments.Record)]
    public async Task<ActionResult<RecordPaymentResponse>> RecordPayment(
        [FromBody] RecordPaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.CurrentOrgId.HasValue)
            return BadRequest(new { error = "Organization context not set." });

        var orgId = _tenantContext.CurrentOrgId.Value;

        var tenancy = await _dbContext.Tenancies
            .FirstOrDefaultAsync(t => t.Id == request.TenancyId && t.OrganizationId == orgId, cancellationToken);

        if (tenancy == null)
            return NotFound(new { error = "Tenancy not found." });

        try
        {
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

                _dbContext.DepositPayments.Add(depositPayment);
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
                    CollectedBy = request.CollectedBy
                };

                _dbContext.RentPayments.Add(rentPayment);
                response.RentPaymentId = rentPayment.Id;
            }

            LogAudit(AuditEventTypes.PaymentRecorded, orgId, nameof(Tenancy), tenancy.Id.ToString(),
                metadata: new { response.DepositPaymentId, response.RentPaymentId, request.DepositAmount, request.RentAmount });
            await _dbContext.SaveChangesAsync(cancellationToken);

            response.Success = true;
            return Ok(response);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Failed to record payment.", detail = ex.Message });
        }
    }

    [HttpGet("totals/deposit/{tenancyId:guid}")]
    [Authorize(Policy = Permissions.Payments.View)]
    public async Task<ActionResult<decimal>> GetTotalDepositPaid(
        Guid tenancyId,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.CurrentOrgId.HasValue)
            return BadRequest(new { error = "Organization context not set." });

        var orgId = _tenantContext.CurrentOrgId.Value;
        try
        {
            // Sum deposit across same (tenant, house) group so record-payment dialog shows correct remaining
            var groupIds = await _rentLedgerService.GetTenancyIdsInSameGroupAsync(tenancyId, cancellationToken);
            if (groupIds == null || groupIds.Count == 0)
                return NotFound(new { error = "Tenancy not found." });

            var total = await _dbContext.DepositPayments
                .Where(p => p.OrganizationId == orgId && groupIds.Contains(p.TenancyId) && !p.IsVoided)
                .SumAsync(p => p.AmountPaid, cancellationToken);

            return Ok(total);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Failed to get deposit total.", detail = ex.Message });
        }
    }

    [HttpGet("totals/rent/{tenancyId:guid}")]
    [Authorize(Policy = Permissions.Payments.View)]
    public async Task<ActionResult<decimal>> GetTotalRentPaidForPeriod(
        Guid tenancyId,
        [FromQuery] int year,
        [FromQuery] int month,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.CurrentOrgId.HasValue)
            return BadRequest(new { error = "Organization context not set." });

        var orgId = _tenantContext.CurrentOrgId.Value;
        try
        {
            // Use same (tenant, house) group and same month range as rent ledger
            var groupIds = await _rentLedgerService.GetTenancyIdsInSameGroupAsync(tenancyId, cancellationToken);
            if (groupIds == null || groupIds.Count == 0)
                return NotFound(new { error = "Tenancy not found." });

            var periodStartUtc = DateTime.SpecifyKind(new DateTime(year, month, 1, 0, 0, 0), DateTimeKind.Utc);
            var periodEndExclusiveUtc = periodStartUtc.AddMonths(1);
            var total = await _dbContext.RentPayments
                .Where(p => p.OrganizationId == orgId &&
                            groupIds.Contains(p.TenancyId) &&
                            p.DatePaid >= periodStartUtc &&
                            p.DatePaid < periodEndExclusiveUtc &&
                            !p.IsVoided)
                .SumAsync(p => p.AmountPaid, cancellationToken);

            return Ok(total);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Failed to get rent total for period.", detail = ex.Message });
        }
    }

    [HttpGet("status/deposit/{tenancyId:guid}")]
    [Authorize(Policy = Permissions.Payments.View)]
    public async Task<ActionResult<string>> GetDepositStatus(
        Guid tenancyId,
        [FromQuery] decimal? requiredAmount,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.CurrentOrgId.HasValue)
            return BadRequest(new { error = "Organization context not set." });

        var orgId = _tenantContext.CurrentOrgId.Value;
        var tenancy = await _dbContext.Tenancies
            .FirstOrDefaultAsync(t => t.Id == tenancyId && t.OrganizationId == orgId, cancellationToken);

        if (tenancy == null)
            return NotFound(new { error = "Tenancy not found." });

        var target = requiredAmount ?? tenancy.DepositAmount;
        var total = await _dbContext.DepositPayments
            .Where(p => p.OrganizationId == orgId && p.TenancyId == tenancyId && !p.IsVoided)
            .SumAsync(p => p.AmountPaid, cancellationToken);

        var status = total >= target
            ? "Paid"
            : total > 0
                ? "PartPaid"
                : "Unpaid";

        return Ok(status);
    }

    [HttpGet("status/rent/{tenancyId:guid}")]
    [Authorize(Policy = Permissions.Payments.View)]
    public async Task<ActionResult<string>> GetRentStatusForPeriod(
        Guid tenancyId,
        [FromQuery] int year,
        [FromQuery] int month,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.CurrentOrgId.HasValue)
            return BadRequest(new { error = "Organization context not set." });

        var orgId = _tenantContext.CurrentOrgId.Value;
        var tenancy = await _dbContext.Tenancies
            .FirstOrDefaultAsync(t => t.Id == tenancyId && t.OrganizationId == orgId, cancellationToken);

        if (tenancy == null)
            return NotFound(new { error = "Tenancy not found." });

        // Use same (tenant, house) group and same month range as rent ledger
        var groupIds = await _rentLedgerService.GetTenancyIdsInSameGroupAsync(tenancyId, cancellationToken);
        var paid = 0m;
        if (groupIds != null && groupIds.Count > 0)
        {
            var periodStartUtc = DateTime.SpecifyKind(new DateTime(year, month, 1, 0, 0, 0), DateTimeKind.Utc);
            var periodEndExclusiveUtc = periodStartUtc.AddMonths(1);
            paid = await _dbContext.RentPayments
                .Where(p => p.OrganizationId == orgId &&
                            groupIds.Contains(p.TenancyId) &&
                            p.DatePaid >= periodStartUtc &&
                            p.DatePaid < periodEndExclusiveUtc &&
                            !p.IsVoided)
                .SumAsync(p => p.AmountPaid, cancellationToken);
        }

        var due = tenancy.RentAmountMonthly;
        var status = paid >= due
            ? "Paid"
            : paid > 0
                ? "PartPaid"
                : "Unpaid";

        return Ok(status);
    }

    [HttpGet("ledger/rent")]
    [Authorize(Policy = Permissions.Payments.View)]
    public async Task<ActionResult<IEnumerable<RentLedgerItemResponse>>> GetRentLedger(
        [FromQuery] int year,
        [FromQuery] int month,
        [FromQuery] Guid? houseId = null,
        [FromQuery] string? statusFilter = null,
        [FromQuery] string? searchTerm = null,
        CancellationToken cancellationToken = default)
    {
        var entries = await _rentLedgerService.GetRentLedgerEntriesAsync(year, month, houseId, statusFilter, searchTerm, cancellationToken);
        return Ok(entries);
    }

    [HttpGet("ledger/deposit")]
    [Authorize(Policy = Permissions.Payments.View)]
    public async Task<ActionResult<IEnumerable<DepositLedgerItemResponse>>> GetDepositLedger(
        [FromQuery] int year,
        [FromQuery] int month,
        [FromQuery] Guid? houseId = null,
        [FromQuery] string? statusFilter = null,
        [FromQuery] string? searchTerm = null,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.CurrentOrgId.HasValue)
            return BadRequest(new { error = "Organization context not set." });

        var orgId = _tenantContext.CurrentOrgId.Value;

        var periodEnd = DateTime.SpecifyKind(new DateTime(year, month, DateTime.DaysInMonth(year, month)), DateTimeKind.Utc);
        var tenanciesQuery = _dbContext.Tenancies
            .AsNoTracking()
            .Include(t => t.Tenant)
            .Include(t => t.House)
            .Where(t => t.OrganizationId == orgId)
            .Where(t => !t.Tenant.IsArchived)
            .Where(t => t.MoveInDate <= periodEnd);

        if (houseId.HasValue)
        {
            tenanciesQuery = tenanciesQuery.Where(t => t.HouseId == houseId.Value);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var search = searchTerm.Trim().ToLower();
            tenanciesQuery = tenanciesQuery.Where(t =>
                t.Tenant.FullName.ToLower().Contains(search) ||
                t.House.AddressLine1.ToLower().Contains(search));
        }

        var tenancies = await tenanciesQuery.ToListAsync(cancellationToken);
        var tenancyIds = tenancies.Select(t => t.Id).Distinct().ToList();

        var depositPayments = await _dbContext.DepositPayments
            .AsNoTracking()
            .Where(p => p.OrganizationId == orgId && tenancyIds.Contains(p.TenancyId) && !p.IsVoided)
            .OrderByDescending(p => p.DatePaid)
            .ToListAsync(cancellationToken);

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

        return Ok(result.OrderBy(r => r.HouseAddress).ThenBy(r => r.TenantName));
    }

    [HttpGet("transactions")]
    [Authorize(Policy = Permissions.Payments.View)]
    public async Task<ActionResult<IEnumerable<TransactionItemResponse>>> GetTransactions(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] string? paymentType = null,
        [FromQuery] Guid? houseId = null,
        [FromQuery] Guid? tenantId = null,
        [FromQuery] string? method = null,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.CurrentOrgId.HasValue)
            return BadRequest(new { error = "Organization context not set." });

        var orgId = _tenantContext.CurrentOrgId.Value;
        var includeRent = string.IsNullOrWhiteSpace(paymentType) || paymentType == "All" || paymentType == "Rent";
        var includeDeposit = string.IsNullOrWhiteSpace(paymentType) || paymentType == "All" || paymentType == "Deposit";
        var normalizedMethod = NormalizeMethod(method);
        var startDateUtc = startDate?.ToUniversalTime();
        var endDateUtc = endDate?.ToUniversalTime();
        var endDateIsDateOnly = endDate.HasValue && endDate.Value.TimeOfDay == TimeSpan.Zero;
        var endDateExclusiveUtc = endDateIsDateOnly && endDateUtc.HasValue
            ? DateTime.SpecifyKind(endDateUtc.Value.Date.AddDays(1), DateTimeKind.Utc)
            : endDateUtc;
        var transactions = new List<TransactionItemResponse>();

        if (includeRent)
        {
            var rentQuery = _dbContext.RentPayments
                .AsNoTracking()
                .Include(p => p.Tenancy)
                    .ThenInclude(t => t.Tenant)
                .Include(p => p.Tenancy)
                    .ThenInclude(t => t.House)
                .Where(p => p.OrganizationId == orgId && !p.IsVoided)
                .AsQueryable();

            if (startDateUtc.HasValue) rentQuery = rentQuery.Where(p => p.DatePaid >= startDateUtc.Value);
            if (endDateExclusiveUtc.HasValue)
            {
                rentQuery = endDateIsDateOnly
                    ? rentQuery.Where(p => p.DatePaid < endDateExclusiveUtc.Value)
                    : rentQuery.Where(p => p.DatePaid <= endDateExclusiveUtc.Value);
            }
            if (houseId.HasValue) rentQuery = rentQuery.Where(p => p.Tenancy.HouseId == houseId.Value);
            if (tenantId.HasValue) rentQuery = rentQuery.Where(p => p.Tenancy.TenantId == tenantId.Value);
            if (!string.IsNullOrWhiteSpace(normalizedMethod))
                rentQuery = rentQuery.Where(p => NormalizeMethod(p.PaymentMethod) == normalizedMethod);

            var rentPayments = await rentQuery.ToListAsync(cancellationToken);
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
            var depositQuery = _dbContext.DepositPayments
                .AsNoTracking()
                .Include(p => p.Tenancy)
                    .ThenInclude(t => t.Tenant)
                .Include(p => p.Tenancy)
                    .ThenInclude(t => t.House)
                .Where(p => p.OrganizationId == orgId && !p.IsVoided)
                .AsQueryable();

            if (startDateUtc.HasValue) depositQuery = depositQuery.Where(p => p.DatePaid >= startDateUtc.Value);
            if (endDateExclusiveUtc.HasValue)
            {
                depositQuery = endDateIsDateOnly
                    ? depositQuery.Where(p => p.DatePaid < endDateExclusiveUtc.Value)
                    : depositQuery.Where(p => p.DatePaid <= endDateExclusiveUtc.Value);
            }
            if (houseId.HasValue) depositQuery = depositQuery.Where(p => p.Tenancy.HouseId == houseId.Value);
            if (tenantId.HasValue) depositQuery = depositQuery.Where(p => p.Tenancy.TenantId == tenantId.Value);
            if (!string.IsNullOrWhiteSpace(normalizedMethod))
                depositQuery = depositQuery.Where(p => NormalizeMethod(p.PaymentMethod) == normalizedMethod);

            var depositPayments = await depositQuery.ToListAsync(cancellationToken);
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

        return Ok(transactions.OrderByDescending(t => t.PaidOn));
    }

    [HttpGet("deposit-return-reminders")]
    [Authorize(Policy = Permissions.Payments.View)]
    public async Task<ActionResult<IEnumerable<DepositReturnReminderResponse>>> GetDepositReturnReminders(CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.CurrentOrgId.HasValue)
            return BadRequest(new { error = "Organization context not set." });

        var orgId = _tenantContext.CurrentOrgId.Value;

        var endedTenancyIdsWithReturn = await _dbContext.DepositReturns
            .AsNoTracking()
            .Where(r => r.OrganizationId == orgId)
            .Select(r => r.TenancyId)
            .Distinct()
            .ToListAsync(cancellationToken);

        // Only show tenancies that have actually ended with a real leave date (exclude default/sentinel like 01/01/0001)
        var minValidLeaveYear = 2000;
        var endedTenancies = await _dbContext.Tenancies
            .AsNoTracking()
            .Include(t => t.Tenant)
            .Include(t => t.House)
            .Where(t => t.OrganizationId == orgId && t.MoveOutDate.HasValue && t.MoveOutDate.Value.Year >= minValidLeaveYear && t.DepositAmount > 0)
            .Where(t => !endedTenancyIdsWithReturn.Contains(t.Id))
            .ToListAsync(cancellationToken);

        var tenancyIds = endedTenancies.Select(t => t.Id).ToList();
        var depositTotals = await _dbContext.DepositPayments
            .AsNoTracking()
            .Where(p => p.OrganizationId == orgId && tenancyIds.Contains(p.TenancyId))
            .GroupBy(p => p.TenancyId)
            .Select(g => new { TenancyId = g.Key, Total = g.Sum(x => x.AmountPaid) })
            .ToDictionaryAsync(x => x.TenancyId, x => x.Total, cancellationToken);

        var result = endedTenancies
            .Where(t => t.Tenant != null && t.House != null && depositTotals.TryGetValue(t.Id, out var total) && total > 0)
            .Select(t => new DepositReturnReminderResponse
            {
                TenancyId = t.Id,
                TenantName = t.Tenant!.FullName,
                HouseAddress = $"{t.House!.AddressLine1}, {t.House.City}",
                LeaveDate = t.MoveOutDate!.Value,
                AmountToReturn = depositTotals[t.Id]
            })
            .OrderBy(r => r.LeaveDate)
            .ToList();

        return Ok(result);
    }

    [HttpPost("deposit-returned")]
    [Authorize(Policy = Permissions.Payments.Record)]
    public async Task<ActionResult> RecordDepositReturned([FromBody] RecordDepositReturnedRequest request, CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.CurrentOrgId.HasValue)
            return BadRequest(new { error = "Organization context not set." });

        var orgId = _tenantContext.CurrentOrgId.Value;

        if (request.AmountReturned < 0)
            return BadRequest(new { error = "Amount returned cannot be negative." });

        if (request.ReturnedDate.Year < 2000)
            return BadRequest(new { error = "Returned date must be a valid date (year >= 2000)." });

        var tenancy = await _dbContext.Tenancies
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == request.TenancyId && t.OrganizationId == orgId, cancellationToken);

        if (tenancy == null)
            return NotFound(new { error = "Tenancy not found." });

        var alreadyReturned = await _dbContext.DepositReturns
            .AnyAsync(r => r.TenancyId == request.TenancyId && r.OrganizationId == orgId, cancellationToken);
        if (alreadyReturned)
            return Conflict(new { error = "A deposit return has already been recorded for this tenancy." });

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

        _dbContext.DepositReturns.Add(depositReturn);
        LogAudit(AuditEventTypes.DepositReturnRecorded, orgId, nameof(Tenancy), tenancy.Id.ToString(),
            metadata: new { depositReturn.AmountReturned });
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Failed to save deposit return.", detail = ex.Message });
        }

        return NoContent();
    }

    // Voids rather than hard-deletes (task #44): financial records are kept for history, just
    // excluded from ledgers/totals/transactions everywhere those queries filter !IsVoided.
    [HttpDelete("transactions")]
    [Authorize(Policy = Permissions.Payments.Void)]
    public async Task<IActionResult> DeleteAllTransactions(CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.CurrentOrgId.HasValue)
            return BadRequest(new { error = "Organization context not set." });

        var orgId = _tenantContext.CurrentOrgId.Value;

        // ExecuteUpdateAsync bypasses the change tracker, so it commits immediately and independently
        // of the audit entry below -- the audit log is staged and saved separately afterward.
        var rentVoided = await _dbContext.RentPayments.Where(p => p.OrganizationId == orgId && !p.IsVoided)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.IsVoided, true), cancellationToken);
        var depositVoided = await _dbContext.DepositPayments.Where(p => p.OrganizationId == orgId && !p.IsVoided)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.IsVoided, true), cancellationToken);

        LogAudit(AuditEventTypes.PaymentsBulkVoided, orgId, nameof(Organization), orgId.ToString(),
            metadata: new { rentPaymentsVoided = rentVoided, depositPaymentsVoided = depositVoided });
        await _dbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    // Voids rather than hard-deletes (task #44): see DeleteAllTransactions above.
    [HttpDelete("transactions/{paymentType}/{paymentId:guid}")]
    [Authorize(Policy = Permissions.Payments.Void)]
    public async Task<IActionResult> UnrecordPayment(
        string paymentType,
        Guid paymentId,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.CurrentOrgId.HasValue)
            return BadRequest(new { error = "Organization context not set." });

        var orgId = _tenantContext.CurrentOrgId.Value;

        if (paymentType.Equals("Rent", StringComparison.OrdinalIgnoreCase))
        {
            var payment = await _dbContext.RentPayments.FirstOrDefaultAsync(p => p.Id == paymentId, cancellationToken);
            if (payment == null)
            {
                return NotFound();
            }

            payment.IsVoided = true;
            LogAudit(AuditEventTypes.PaymentVoided, orgId, nameof(RentPayment), payment.Id.ToString(),
                metadata: new { paymentType = "Rent", payment.AmountPaid });
            await _dbContext.SaveChangesAsync(cancellationToken);
            return NoContent();
        }

        if (paymentType.Equals("Deposit", StringComparison.OrdinalIgnoreCase))
        {
            var payment = await _dbContext.DepositPayments.FirstOrDefaultAsync(p => p.Id == paymentId, cancellationToken);
            if (payment == null)
            {
                return NotFound();
            }

            payment.IsVoided = true;
            LogAudit(AuditEventTypes.PaymentVoided, orgId, nameof(DepositPayment), payment.Id.ToString(),
                metadata: new { paymentType = "Deposit", payment.AmountPaid });
            await _dbContext.SaveChangesAsync(cancellationToken);
            return NoContent();
        }

        return BadRequest(new { error = "paymentType must be 'Rent' or 'Deposit'." });
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
}

public class RecordPaymentRequest
{
    public Guid TenancyId { get; set; }
    public decimal DepositAmount { get; set; }
    public decimal RentAmount { get; set; }
    public int RentYear { get; set; }
    public int RentMonth { get; set; }
    public DateTime PaymentDate { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public string? Reference { get; set; }
    public string? Notes { get; set; }
    public string? CollectedBy { get; set; }
    public bool UseDepositForRent { get; set; }
}

public class RecordPaymentResponse
{
    public bool Success { get; set; }
    public Guid? DepositPaymentId { get; set; }
    public Guid? RentPaymentId { get; set; }
}

public class DepositLedgerItemResponse
{
    public Guid TenancyId { get; set; }
    public Guid TenantId { get; set; }
    public Guid HouseId { get; set; }
    public string HouseAddress { get; set; } = string.Empty;
    public string TenantName { get; set; } = string.Empty;
    public decimal DepositRequired { get; set; }
    public decimal AmountPaid { get; set; }
    public string Status { get; set; } = "Unpaid";
    public List<PaymentDetailApiResponse> Payments { get; set; } = new();
}

public class TransactionItemResponse
{
    public Guid PaymentId { get; set; }
    public string PaymentType { get; set; } = string.Empty;
    public DateTime PaidOn { get; set; }
    public string TenantName { get; set; } = string.Empty;
    public string HouseAddress { get; set; } = string.Empty;
    public string PeriodLabel { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Method { get; set; } = string.Empty;
    public string? Reference { get; set; }
    public string? Notes { get; set; }
    public string? CollectedBy { get; set; }
    public Guid? TenancyId { get; set; }
}

public class DepositReturnReminderResponse
{
    public Guid TenancyId { get; set; }
    public string TenantName { get; set; } = string.Empty;
    public string HouseAddress { get; set; } = string.Empty;
    public DateTime LeaveDate { get; set; }
    public decimal AmountToReturn { get; set; }
}

public class RecordDepositReturnedRequest
{
    public Guid TenancyId { get; set; }
    public DateTime ReturnedDate { get; set; }
    public decimal AmountReturned { get; set; }
    public string? Notes { get; set; }
}
