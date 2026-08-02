using Daryva.Api.Domain;

namespace Daryva.Api.Services.Interfaces;

public interface IExpenseService
{
    Task<IEnumerable<Expense>> GetAllExpensesAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<Expense>> GetExpensesByHouseAsync(Guid houseId, CancellationToken cancellationToken = default);
    Task<Expense?> GetExpenseByIdAsync(Guid expenseId, CancellationToken cancellationToken = default);
    Task<Expense> CreateExpenseAsync(Expense expense, CancellationToken cancellationToken = default);
    Task UpdateExpenseAsync(Expense expense, CancellationToken cancellationToken = default);
    Task DeleteExpenseAsync(Guid expenseId, CancellationToken cancellationToken = default);
}
