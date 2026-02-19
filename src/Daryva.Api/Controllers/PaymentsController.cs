using Daryva.Api.Data;
using Daryva.Api.Domain;
using Daryva.Api.Security;
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

    public PaymentsController(AppDbContext dbContext, ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    [HttpPost("record")]
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

        var response = new RecordPaymentResponse();

        if (!request.UseDepositForRent && request.DepositAmount > 0)
        {
            var depositPayment = new DepositPayment
            {
                Id = Guid.NewGuid(),
                OrganizationId = orgId,
                TenancyId = tenancy.Id,
                DatePaid = request.PaymentDate,
                AmountPaid = request.DepositAmount,
                PaymentMethod = request.PaymentMethod,
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
                DatePaid = request.PaymentDate,
                AmountPaid = request.RentAmount,
                PaymentMethod = request.PaymentMethod,
                ReferenceNumber = request.Reference,
                Notes = request.Notes,
                CollectedBy = request.CollectedBy
            };

            _dbContext.RentPayments.Add(rentPayment);
            response.RentPaymentId = rentPayment.Id;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        response.Success = true;
        return Ok(response);
    }

    [HttpGet("totals/deposit/{tenancyId:guid}")]
    public async Task<ActionResult<decimal>> GetTotalDepositPaid(
        Guid tenancyId,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.CurrentOrgId.HasValue)
            return BadRequest(new { error = "Organization context not set." });

        var orgId = _tenantContext.CurrentOrgId.Value;
        var total = await _dbContext.DepositPayments
            .Where(p => p.OrganizationId == orgId && p.TenancyId == tenancyId)
            .Select(p => p.AmountPaid)
            .DefaultIfEmpty(0m)
            .SumAsync(cancellationToken);

        return Ok(total);
    }

    [HttpGet("totals/rent/{tenancyId:guid}")]
    public async Task<ActionResult<decimal>> GetTotalRentPaidForPeriod(
        Guid tenancyId,
        [FromQuery] int year,
        [FromQuery] int month,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.CurrentOrgId.HasValue)
            return BadRequest(new { error = "Organization context not set." });

        var orgId = _tenantContext.CurrentOrgId.Value;
        var total = await _dbContext.RentPayments
            .Where(p => p.OrganizationId == orgId &&
                        p.TenancyId == tenancyId &&
                        p.DatePaid.Year == year &&
                        p.DatePaid.Month == month)
            .Select(p => p.AmountPaid)
            .DefaultIfEmpty(0m)
            .SumAsync(cancellationToken);

        return Ok(total);
    }

    [HttpGet("status/deposit/{tenancyId:guid}")]
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
            .Where(p => p.OrganizationId == orgId && p.TenancyId == tenancyId)
            .Select(p => p.AmountPaid)
            .DefaultIfEmpty(0m)
            .SumAsync(cancellationToken);

        var status = total >= target
            ? "Paid"
            : total > 0
                ? "PartPaid"
                : "Unpaid";

        return Ok(status);
    }

    [HttpGet("status/rent/{tenancyId:guid}")]
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

        var paid = await _dbContext.RentPayments
            .Where(p => p.OrganizationId == orgId &&
                        p.TenancyId == tenancyId &&
                        p.DatePaid.Year == year &&
                        p.DatePaid.Month == month)
            .Select(p => p.AmountPaid)
            .DefaultIfEmpty(0m)
            .SumAsync(cancellationToken);

        var due = tenancy.RentAmountMonthly;
        var status = paid >= due
            ? "Paid"
            : paid > 0
                ? "PartPaid"
                : "Unpaid";

        return Ok(status);
    }

    [HttpGet("ledger/rent")]
    public async Task<ActionResult<IEnumerable<RentLedgerItemResponse>>> GetRentLedger(
        [FromQuery] int year,
        [FromQuery] int month,
        [FromQuery] Guid? houseId = null,
        [FromQuery] string? statusFilter = null,
        [FromQuery] string? searchTerm = null,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.CurrentOrgId.HasValue)
            return BadRequest(new { error = "Organization context not set." });

        var periodStart = new DateTime(year, month, 1);
        var periodEnd = new DateTime(year, month, DateTime.DaysInMonth(year, month));

        var tenanciesQuery = _dbContext.Tenancies
            .AsNoTracking()
            .Include(t => t.Tenant)
            .Include(t => t.House)
            .Where(t => t.MoveInDate <= periodEnd && (!t.MoveOutDate.HasValue || t.MoveOutDate.Value >= periodStart));

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

        var periodRentPayments = await _dbContext.RentPayments
            .AsNoTracking()
            .Where(p => tenancyIds.Contains(p.TenancyId) && p.DatePaid.Year == year && p.DatePaid.Month == month)
            .OrderByDescending(p => p.DatePaid)
            .ToListAsync(cancellationToken);

        var totalDepositByTenancy = await _dbContext.DepositPayments
            .AsNoTracking()
            .Where(p => tenancyIds.Contains(p.TenancyId))
            .GroupBy(p => p.TenancyId)
            .Select(g => new { g.Key, Amount = g.Sum(x => x.AmountPaid) })
            .ToDictionaryAsync(x => x.Key, x => x.Amount, cancellationToken);

        var result = new List<RentLedgerItemResponse>();
        foreach (var tenancy in tenancies)
        {
            var rentStartYear = tenancy.RentStartYear ?? tenancy.MoveInDate.Year;
            var rentStartMonth = tenancy.RentStartMonth ?? tenancy.MoveInDate.Month;
            var selectedPeriodNum = year * 12 + month;
            var firstRentPeriodNum = rentStartYear * 12 + rentStartMonth;
            if (selectedPeriodNum < firstRentPeriodNum)
            {
                continue;
            }

            var paymentsForPeriod = periodRentPayments.Where(p => p.TenancyId == tenancy.Id).ToList();
            var amountPaid = paymentsForPeriod.Sum(p => p.AmountPaid);
            var amountDue = tenancy.RentAmountMonthly;
            var dueDate = new DateTime(year, month, Math.Min((int)tenancy.PaymentDueDay, 28));

            var status = amountPaid >= amountDue
                ? "Paid"
                : amountPaid > 0
                    ? "PartPaid"
                    : dueDate < DateTime.UtcNow.Date
                        ? "Overdue"
                        : "Unpaid";

            if (!MatchesStatusFilter(statusFilter, status))
            {
                continue;
            }

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

        return Ok(result.OrderBy(r => r.HouseAddress).ThenBy(r => r.TenantName));
    }

    [HttpGet("ledger/deposit")]
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

        var periodEnd = new DateTime(year, month, DateTime.DaysInMonth(year, month));
        var tenanciesQuery = _dbContext.Tenancies
            .AsNoTracking()
            .Include(t => t.Tenant)
            .Include(t => t.House)
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
            .Where(p => tenancyIds.Contains(p.TenancyId))
            .OrderByDescending(p => p.DatePaid)
            .ToListAsync(cancellationToken);

        var result = new List<DepositLedgerItemResponse>();
        foreach (var tenancy in tenancies)
        {
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

        var includeRent = string.IsNullOrWhiteSpace(paymentType) || paymentType == "All" || paymentType == "Rent";
        var includeDeposit = string.IsNullOrWhiteSpace(paymentType) || paymentType == "All" || paymentType == "Deposit";
        var normalizedMethod = NormalizeMethod(method);
        var transactions = new List<TransactionItemResponse>();

        if (includeRent)
        {
            var rentQuery = _dbContext.RentPayments
                .AsNoTracking()
                .Include(p => p.Tenancy)
                    .ThenInclude(t => t.Tenant)
                .Include(p => p.Tenancy)
                    .ThenInclude(t => t.House)
                .AsQueryable();

            if (startDate.HasValue) rentQuery = rentQuery.Where(p => p.DatePaid >= startDate.Value);
            if (endDate.HasValue) rentQuery = rentQuery.Where(p => p.DatePaid <= endDate.Value);
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
                .AsQueryable();

            if (startDate.HasValue) depositQuery = depositQuery.Where(p => p.DatePaid >= startDate.Value);
            if (endDate.HasValue) depositQuery = depositQuery.Where(p => p.DatePaid <= endDate.Value);
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

    [HttpDelete("transactions/{paymentType}/{paymentId:guid}")]
    public async Task<IActionResult> UnrecordPayment(
        string paymentType,
        Guid paymentId,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.CurrentOrgId.HasValue)
            return BadRequest(new { error = "Organization context not set." });

        if (paymentType.Equals("Rent", StringComparison.OrdinalIgnoreCase))
        {
            var payment = await _dbContext.RentPayments.FirstOrDefaultAsync(p => p.Id == paymentId, cancellationToken);
            if (payment == null)
            {
                return NotFound();
            }

            _dbContext.RentPayments.Remove(payment);
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

            _dbContext.DepositPayments.Remove(payment);
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

public class PaymentDetailApiResponse
{
    public Guid PaymentId { get; set; }
    public DateTime PaidOn { get; set; }
    public decimal Amount { get; set; }
    public string Method { get; set; } = string.Empty;
    public string? Reference { get; set; }
    public string? Notes { get; set; }
    public string? CollectedBy { get; set; }
}

public class RentLedgerItemResponse
{
    public Guid TenancyId { get; set; }
    public Guid TenantId { get; set; }
    public Guid HouseId { get; set; }
    public string HouseAddress { get; set; } = string.Empty;
    public string TenantName { get; set; } = string.Empty;
    public DateTime DueDate { get; set; }
    public decimal AmountDue { get; set; }
    public decimal AmountPaid { get; set; }
    public string Status { get; set; } = "Unpaid";
    public decimal DepositRemaining { get; set; }
    public List<PaymentDetailApiResponse> PaymentsForThisMonth { get; set; } = new();
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
