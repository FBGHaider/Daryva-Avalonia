using Daryva.Api.Domain;

namespace Daryva.Api.Services.Interfaces;

public interface IExpenseService
{
    /// <param name="includeArchived">When false, archived expenses are excluded.</param>
    Task<IEnumerable<Expense>> GetAllExpensesAsync(bool includeArchived = false, CancellationToken cancellationToken = default);
    /// <param name="includeArchived">When false, archived expenses are excluded.</param>
    Task<IEnumerable<Expense>> GetExpensesByHouseAsync(Guid houseId, bool includeArchived = false, CancellationToken cancellationToken = default);
    Task<Expense?> GetExpenseByIdAsync(Guid expenseId, CancellationToken cancellationToken = default);
    Task<Expense> CreateExpenseAsync(Expense expense, CancellationToken cancellationToken = default);
    Task UpdateExpenseAsync(Expense expense, CancellationToken cancellationToken = default);
    /// <summary>Archives (soft-deletes) the expense -- see task #44. Financial records are kept, not hard-deleted.</summary>
    Task ArchiveExpenseAsync(Guid expenseId, CancellationToken cancellationToken = default);
}
