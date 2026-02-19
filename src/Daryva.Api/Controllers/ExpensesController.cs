using Daryva.Api.Domain;
using Daryva.Api.Security;
using Daryva.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Daryva.Api.Controllers;

[ApiController]
[Route("api/expenses")]
[Authorize]
public class ExpensesController : ControllerBase
{
    private readonly IExpenseService _expenseService;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<ExpensesController> _logger;

    public ExpensesController(
        IExpenseService expenseService,
        ITenantContext tenantContext,
        ILogger<ExpensesController> logger)
    {
        _expenseService = expenseService;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ExpenseResponse>>> GetExpenses(CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.CurrentOrgId.HasValue)
            return BadRequest(new { error = "Organization context not set." });

        var expenses = await _expenseService.GetAllExpensesAsync(cancellationToken);
        var response = expenses.Select(MapToResponse).ToList();
        return Ok(response);
    }

    [HttpGet("house/{houseId:guid}")]
    public async Task<ActionResult<IEnumerable<ExpenseResponse>>> GetExpensesByHouse(
        Guid houseId,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.CurrentOrgId.HasValue)
            return BadRequest(new { error = "Organization context not set." });

        var expenses = await _expenseService.GetExpensesByHouseAsync(houseId, cancellationToken);
        var response = expenses.Select(MapToResponse).ToList();
        return Ok(response);
    }

    [HttpGet("{expenseId:guid}")]
    public async Task<ActionResult<ExpenseResponse>> GetExpense(
        Guid expenseId,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.CurrentOrgId.HasValue)
            return BadRequest(new { error = "Organization context not set." });

        var expense = await _expenseService.GetExpenseByIdAsync(expenseId, cancellationToken);
        if (expense == null)
            return NotFound();

        return Ok(MapToResponse(expense));
    }

    [HttpPost]
    public async Task<ActionResult<ExpenseResponse>> CreateExpense(
        [FromBody] CreateExpenseRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.CurrentOrgId.HasValue)
            return BadRequest(new { error = "Organization context not set." });

        var expense = new Expense
        {
            Id = Guid.NewGuid(),
            OrganizationId = _tenantContext.CurrentOrgId.Value,
            HouseId = request.HouseId,
            DateIncurred = request.DateIncurred,
            Category = request.Category,
            Amount = request.Amount,
            Vendor = request.Vendor,
            Notes = request.Notes,
            ReceiptDocumentId = request.ReceiptDocumentId
        };

        var created = await _expenseService.CreateExpenseAsync(expense, cancellationToken);
        return CreatedAtAction(nameof(GetExpense), new { expenseId = created.Id }, MapToResponse(created));
    }

    [HttpPut("{expenseId:guid}")]
    public async Task<ActionResult<ExpenseResponse>> UpdateExpense(
        Guid expenseId,
        [FromBody] UpdateExpenseRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.CurrentOrgId.HasValue)
            return BadRequest(new { error = "Organization context not set." });

        var expense = await _expenseService.GetExpenseByIdAsync(expenseId, cancellationToken);
        if (expense == null)
            return NotFound();

        expense.DateIncurred = request.DateIncurred;
        expense.Category = request.Category;
        expense.Amount = request.Amount;
        expense.Vendor = request.Vendor;
        expense.Notes = request.Notes;
        expense.ReceiptDocumentId = request.ReceiptDocumentId;

        await _expenseService.UpdateExpenseAsync(expense, cancellationToken);
        return Ok(MapToResponse(expense));
    }

    [HttpDelete("{expenseId:guid}")]
    public async Task<IActionResult> DeleteExpense(
        Guid expenseId,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.CurrentOrgId.HasValue)
            return BadRequest(new { error = "Organization context not set." });

        var expense = await _expenseService.GetExpenseByIdAsync(expenseId, cancellationToken);
        if (expense == null)
            return NotFound();

        await _expenseService.DeleteExpenseAsync(expenseId, cancellationToken);
        return NoContent();
    }

    private static ExpenseResponse MapToResponse(Expense expense)
    {
        return new ExpenseResponse
        {
            Id = expense.Id,
            HouseId = expense.HouseId,
            HouseAddress = expense.House?.AddressLine1 ?? string.Empty,
            DateIncurred = expense.DateIncurred,
            Category = expense.Category,
            Amount = expense.Amount,
            Vendor = expense.Vendor,
            Notes = expense.Notes,
            ReceiptDocumentId = expense.ReceiptDocumentId
        };
    }
}

public class CreateExpenseRequest
{
    public Guid HouseId { get; set; }
    public DateTime DateIncurred { get; set; }
    public string Category { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? Vendor { get; set; }
    public string? Notes { get; set; }
    public Guid? ReceiptDocumentId { get; set; }
}

public class UpdateExpenseRequest
{
    public DateTime DateIncurred { get; set; }
    public string Category { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? Vendor { get; set; }
    public string? Notes { get; set; }
    public Guid? ReceiptDocumentId { get; set; }
}

public class ExpenseResponse
{
    public Guid Id { get; set; }
    public Guid HouseId { get; set; }
    public string HouseAddress { get; set; } = string.Empty;
    public DateTime DateIncurred { get; set; }
    public string Category { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? Vendor { get; set; }
    public string? Notes { get; set; }
    public Guid? ReceiptDocumentId { get; set; }
}
