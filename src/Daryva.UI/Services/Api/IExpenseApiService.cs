namespace Daryva.Services.Api;

/// <summary>
/// DTO for Expense API responses.
/// </summary>
public class ExpenseDto
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

/// <summary>
/// DTO for creating a new expense.
/// </summary>
public class CreateExpenseDto
{
    public Guid HouseId { get; set; }
    public DateTime DateIncurred { get; set; }
    public string Category { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? Vendor { get; set; }
    public string? Notes { get; set; }
    public Guid? ReceiptDocumentId { get; set; }
}

/// <summary>
/// DTO for updating an expense.
/// </summary>
public class UpdateExpenseDto
{
    public DateTime DateIncurred { get; set; }
    public string Category { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? Vendor { get; set; }
    public string? Notes { get; set; }
    public Guid? ReceiptDocumentId { get; set; }
}

/// <summary>
/// Service for expense-related API operations.
/// </summary>
public interface IExpenseApiService
{
    /// <summary>
    /// Get all expenses for the current organization.
    /// </summary>
    Task<List<ExpenseDto>> GetExpensesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get expenses for a specific house.
    /// </summary>
    Task<List<ExpenseDto>> GetExpensesByHouseAsync(Guid houseId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a specific expense by ID.
    /// </summary>
    Task<ExpenseDto?> GetExpenseAsync(Guid expenseId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a new expense in the current organization.
    /// </summary>
    Task<ExpenseDto> CreateExpenseAsync(CreateExpenseDto expense, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update an existing expense.
    /// </summary>
    Task<ExpenseDto> UpdateExpenseAsync(Guid expenseId, UpdateExpenseDto expense, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete an expense.
    /// </summary>
    Task<bool> DeleteExpenseAsync(Guid expenseId, CancellationToken cancellationToken = default);
}
