using Daryva.MVVM.Models;
using Daryva.Services.Api;

namespace Daryva.Services.Business;

/// <summary>
/// Adapter that implements IExpenseService using the backend API.
/// Maps between UI HouseExpense model and API ExpenseDto.
/// Replaces the SQLite-based ExpenseService when using API backend.
/// </summary>
public class ExpenseApiServiceAdapter : IExpenseService
{
    private readonly IExpenseApiService _expenseApiService;
    private readonly IHouseApiService _houseApiService;

    public ExpenseApiServiceAdapter(IExpenseApiService expenseApiService, IHouseApiService houseApiService)
    {
        _expenseApiService = expenseApiService ?? throw new ArgumentNullException(nameof(expenseApiService));
        _houseApiService = houseApiService ?? throw new ArgumentNullException(nameof(houseApiService));
    }

    public async Task<IEnumerable<HouseExpense>> GetExpensesAsync(int? houseId = null, DateTime? startDate = null, DateTime? endDate = null, string? category = null, string? searchTerm = null)
    {
        var expenseDtos = await _expenseApiService.GetExpensesAsync();
        var expenses = expenseDtos.Select(MapToExpense).ToList();

        // Filter client-side
        if (houseId.HasValue)
            expenses = expenses.Where(e => e.HouseId == houseId.Value).ToList();

        if (startDate.HasValue)
            expenses = expenses.Where(e => e.DateIncurred >= startDate.Value).ToList();

        if (endDate.HasValue)
            expenses = expenses.Where(e => e.DateIncurred <= endDate.Value).ToList();

        if (!string.IsNullOrWhiteSpace(category))
            expenses = expenses.Where(e => e.Category.Equals(category, StringComparison.OrdinalIgnoreCase)).ToList();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var lowerSearch = searchTerm.ToLowerInvariant();
            expenses = expenses.Where(e =>
                (e.Vendor?.ToLowerInvariant().Contains(lowerSearch) ?? false) ||
                (e.Notes?.ToLowerInvariant().Contains(lowerSearch) ?? false) ||
                e.Category.ToLowerInvariant().Contains(lowerSearch)).ToList();
        }

        return expenses;
    }

    public async Task<HouseExpense?> GetExpenseByIdAsync(int expenseId)
    {
        var expenses = await GetExpensesAsync();
        return expenses.FirstOrDefault(e => e.HouseExpenseId == expenseId);
    }

    public async Task<HouseExpense> CreateExpenseAsync(HouseExpense expense)
    {
        var createDto = new CreateExpenseDto
        {
            HouseId = expense.ApiHouseId ?? throw new InvalidOperationException("Expense must have API House ID"),
            DateIncurred = expense.DateIncurred,
            Category = expense.Category,
            Amount = expense.Amount,
            Vendor = expense.Vendor,
            Notes = expense.Notes,
            ReceiptDocumentId = expense.ReceiptDocumentId.HasValue ? MapToGuid(expense.ReceiptDocumentId.Value) : null
        };

        var createdDto = await _expenseApiService.CreateExpenseAsync(createDto);
        return MapToExpense(createdDto);
    }

    public async Task UpdateExpenseAsync(HouseExpense expense)
    {
        if (!expense.ApiId.HasValue)
            throw new InvalidOperationException("Cannot update expense without API ID.");

        var updateDto = new UpdateExpenseDto
        {
            DateIncurred = expense.DateIncurred,
            Category = expense.Category,
            Amount = expense.Amount,
            Vendor = expense.Vendor,
            Notes = expense.Notes,
            ReceiptDocumentId = expense.ReceiptDocumentId.HasValue ? MapToGuid(expense.ReceiptDocumentId.Value) : null
        };

        var updatedDto = await _expenseApiService.UpdateExpenseAsync(expense.ApiId.Value, updateDto);
        
        // Update the expense object with response data
        expense.DateIncurred = updatedDto.DateIncurred;
        expense.Category = updatedDto.Category;
        expense.Amount = updatedDto.Amount;
        expense.Vendor = updatedDto.Vendor;
        expense.Notes = updatedDto.Notes;
    }

    public async Task DeleteExpenseAsync(int expenseId)
    {
        var expense = await GetExpenseByIdAsync(expenseId);
        if (expense == null || !expense.ApiId.HasValue)
            throw new InvalidOperationException($"Expense with ID {expenseId} not found or has no API ID.");

        var deleted = await _expenseApiService.DeleteExpenseAsync(expense.ApiId.Value);
        if (!deleted)
            throw new InvalidOperationException($"Failed to delete expense with ID {expenseId}.");
    }

    public async Task<ExpenseSummary> GetExpenseSummaryAsync(int? houseId = null, DateTime? startDate = null, DateTime? endDate = null, string? category = null, string? searchTerm = null)
    {
        var expenses = (await GetExpensesAsync(houseId, startDate, endDate, category, searchTerm)).ToList();
        
        var summary = new ExpenseSummary
        {
            TotalAmount = expenses.Sum(e => e.Amount),
            TotalCount = expenses.Count,
            HighestSingleExpense = expenses.Any() ? expenses.Max(e => e.Amount) : 0m
        };

        if (expenses.Any())
        {
            var months = (int)Math.Ceiling((expenses.Max(e => e.DateIncurred) - expenses.Min(e => e.DateIncurred)).TotalDays / 30.0);
            summary.AverageMonthlyAmount = months > 0 ? summary.TotalAmount / months : summary.TotalAmount;

            var byCategory = expenses.GroupBy(e => e.Category)
                .OrderByDescending(g => g.Sum(e => e.Amount))
                .FirstOrDefault();
            
            if (byCategory != null)
            {
                summary.BiggestCategory = byCategory.Key;
                summary.BiggestCategoryAmount = byCategory.Sum(e => e.Amount);
            }
        }

        return summary;
    }

    public async Task<string> ExportExpensesToCsvAsync(int? houseId = null, DateTime? startDate = null, DateTime? endDate = null, string? category = null)
    {
        // TODO: Implement CSV export if needed
        return await Task.FromResult(string.Empty);
    }

    /// <summary>
    /// Map ExpenseDto from API to UI HouseExpense model.
    /// Assigns a local int ID based on the hash of the Guid.
    /// </summary>
    private HouseExpense MapToExpense(ExpenseDto dto)
    {
        return new HouseExpense
        {
            HouseExpenseId = dto.Id.GetHashCode(),
            ApiId = dto.Id,
            HouseId = dto.HouseId.GetHashCode(),
            ApiHouseId = dto.HouseId,
            HouseAddress = dto.HouseAddress,
            DateIncurred = dto.DateIncurred,
            Category = dto.Category,
            Amount = dto.Amount,
            Vendor = dto.Vendor,
            Notes = dto.Notes,
            ReceiptDocumentId = dto.ReceiptDocumentId.HasValue ? dto.ReceiptDocumentId.Value.GetHashCode() : null
        };
    }

    /// <summary>
    /// Helper to convert int ID to Guid by hashing.
    /// This is a reverse operation for UI to API ID mapping.
    /// </summary>
    private Guid MapToGuid(int id)
    {
        // Create a GUID from the int hash (not reversible, just for consistency)
        var bytes = BitConverter.GetBytes(id);
        Array.Resize(ref bytes, 16);
        return new Guid(bytes);
    }
}
