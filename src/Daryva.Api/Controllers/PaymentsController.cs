using Daryva.Api.Dtos;
using Daryva.Api.Security;
using Daryva.Api.Security.Interfaces;
using Daryva.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Daryva.Api.Controllers;

[ApiController]
[Route("api/payments")]
[Authorize]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentService _paymentService;
    private readonly ITenancyService _tenancyService;
    private readonly ITenantContext _tenantContext;

    public PaymentsController(IPaymentService paymentService, ITenancyService tenancyService, ITenantContext tenantContext)
    {
        _paymentService = paymentService;
        _tenancyService = tenancyService;
        _tenantContext = tenantContext;
    }

    /// <summary>
    /// True if the caller is a Tenant and the given tenancy isn't theirs (or doesn't exist) --
    /// the per-tenancy totals/status endpoints are the only payment views the portal exposes,
    /// so this is the one ownership check that matters here.
    /// </summary>
    private async Task<bool> IsForbiddenTenancyForCaller(Guid tenancyId, CancellationToken cancellationToken)
    {
        if (_tenantContext.CurrentRole != Roles.Tenant)
            return false;

        var tenancy = await _tenancyService.GetTenancyAsync(tenancyId, cancellationToken);
        return tenancy == null || tenancy.TenantId != _tenantContext.CurrentTenantId;
    }

    [HttpPost("record")]
    [Authorize(Policy = Permissions.Payments.Record)]
    public async Task<ActionResult<RecordPaymentResponse>> RecordPayment(
        [FromBody] RecordPaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.CurrentOrgId.HasValue)
            return BadRequest(new { error = "Organization context not set." });

        try
        {
            var response = await _paymentService.RecordPaymentAsync(request, cancellationToken);
            return Ok(response);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
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

        if (await IsForbiddenTenancyForCaller(tenancyId, cancellationToken))
            return Forbid();

        try
        {
            var total = await _paymentService.GetTotalDepositPaidAsync(tenancyId, cancellationToken);
            if (total == null)
                return NotFound(new { error = "Tenancy not found." });

            return Ok(total.Value);
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

        if (await IsForbiddenTenancyForCaller(tenancyId, cancellationToken))
            return Forbid();

        try
        {
            var total = await _paymentService.GetTotalRentPaidForPeriodAsync(tenancyId, year, month, cancellationToken);
            if (total == null)
                return NotFound(new { error = "Tenancy not found." });

            return Ok(total.Value);
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

        if (await IsForbiddenTenancyForCaller(tenancyId, cancellationToken))
            return Forbid();

        var status = await _paymentService.GetDepositStatusAsync(tenancyId, requiredAmount, cancellationToken);
        if (status == null)
            return NotFound(new { error = "Tenancy not found." });

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

        if (await IsForbiddenTenancyForCaller(tenancyId, cancellationToken))
            return Forbid();

        var status = await _paymentService.GetRentStatusForPeriodAsync(tenancyId, year, month, cancellationToken);
        if (status == null)
            return NotFound(new { error = "Tenancy not found." });

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
        // Portfolio-wide view with no natural per-tenant scoping -- not exposed to the portal.
        if (_tenantContext.CurrentRole == Roles.Tenant)
            return Forbid();

        var entries = await _paymentService.GetRentLedgerAsync(year, month, houseId, statusFilter, searchTerm, cancellationToken);
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

        // Portfolio-wide view with no natural per-tenant scoping -- not exposed to the portal.
        if (_tenantContext.CurrentRole == Roles.Tenant)
            return Forbid();

        var result = await _paymentService.GetDepositLedgerAsync(year, month, houseId, statusFilter, searchTerm, cancellationToken);
        return Ok(result);
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

        // Portfolio-wide view with no natural per-tenant scoping -- not exposed to the portal.
        if (_tenantContext.CurrentRole == Roles.Tenant)
            return Forbid();

        var transactions = await _paymentService.GetTransactionsAsync(startDate, endDate, paymentType, houseId, tenantId, method, cancellationToken);
        return Ok(transactions);
    }

    [HttpGet("deposit-return-reminders")]
    [Authorize(Policy = Permissions.Payments.View)]
    public async Task<ActionResult<IEnumerable<DepositReturnReminderResponse>>> GetDepositReturnReminders(CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.CurrentOrgId.HasValue)
            return BadRequest(new { error = "Organization context not set." });

        // Portfolio-wide operational list -- not exposed to the portal.
        if (_tenantContext.CurrentRole == Roles.Tenant)
            return Forbid();

        var result = await _paymentService.GetDepositReturnRemindersAsync(cancellationToken);
        return Ok(result);
    }

    [HttpPost("deposit-returned")]
    [Authorize(Policy = Permissions.Payments.Record)]
    public async Task<ActionResult> RecordDepositReturned([FromBody] RecordDepositReturnedRequest request, CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.CurrentOrgId.HasValue)
            return BadRequest(new { error = "Organization context not set." });

        try
        {
            await _paymentService.RecordDepositReturnedAsync(request, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Failed to save deposit return.", detail = ex.Message });
        }
    }

    [HttpDelete("transactions")]
    [Authorize(Policy = Permissions.Payments.Void)]
    public async Task<IActionResult> DeleteAllTransactions(CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.CurrentOrgId.HasValue)
            return BadRequest(new { error = "Organization context not set." });

        await _paymentService.VoidAllTransactionsAsync(cancellationToken);
        return NoContent();
    }

    [HttpDelete("transactions/{paymentType}/{paymentId:guid}")]
    [Authorize(Policy = Permissions.Payments.Void)]
    public async Task<IActionResult> UnrecordPayment(
        string paymentType,
        Guid paymentId,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.CurrentOrgId.HasValue)
            return BadRequest(new { error = "Organization context not set." });

        try
        {
            var found = await _paymentService.UnrecordPaymentAsync(paymentType, paymentId, cancellationToken);
            return found ? NoContent() : NotFound();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
