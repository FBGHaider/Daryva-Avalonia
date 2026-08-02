using Daryva.Api.Data;
using Daryva.Api.Domain;
using Daryva.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Daryva.Api.Services;

public class ExpenseService : IExpenseService
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<ExpenseService> _logger;

    public ExpenseService(AppDbContext dbContext, ILogger<ExpenseService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<IEnumerable<Expense>> GetAllExpensesAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Expenses
            .Include(e => e.House)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Expense>> GetExpensesByHouseAsync(Guid houseId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Expenses
            .Include(e => e.House)
            .AsNoTracking()
            .Where(e => e.HouseId == houseId)
            .ToListAsync(cancellationToken);
    }

    public async Task<Expense?> GetExpenseByIdAsync(Guid expenseId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Expenses
            .Include(e => e.House)
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == expenseId, cancellationToken);
    }

    public async Task<Expense> CreateExpenseAsync(Expense expense, CancellationToken cancellationToken = default)
    {
        _dbContext.Expenses.Add(expense);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return expense;
    }

    public async Task UpdateExpenseAsync(Expense expense, CancellationToken cancellationToken = default)
    {
        _dbContext.Expenses.Update(expense);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteExpenseAsync(Guid expenseId, CancellationToken cancellationToken = default)
    {
        var expense = await GetExpenseByIdAsync(expenseId, cancellationToken);
        if (expense != null)
        {
            _dbContext.Expenses.Remove(expense);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
