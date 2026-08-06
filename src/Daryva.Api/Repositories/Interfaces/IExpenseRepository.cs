using Daryva.Api.Domain;

namespace Daryva.Api.Repositories.Interfaces;

public interface IExpenseRepository
{
    /// <summary>Includes the House navigation -- callers list expenses alongside their house.</summary>
    Task<List<Expense>> GetAllAsync(bool includeArchived, CancellationToken cancellationToken = default);

    Task<List<Expense>> GetByHouseIdAsync(Guid houseId, bool includeArchived, CancellationToken cancellationToken = default);

    Task<Expense?> GetByIdAsync(Guid expenseId, CancellationToken cancellationToken = default);

    /// <summary>Tracked (no AsNoTracking, no Include) -- for in-place mutation such as archiving.</summary>
    Task<Expense?> GetTrackedByIdAsync(Guid expenseId, CancellationToken cancellationToken = default);

    void Add(Expense expense);

    void Update(Expense expense);
}
