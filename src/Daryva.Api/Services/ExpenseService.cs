using Daryva.Api.Data;
using Daryva.Api.Domain;
using Daryva.Api.Security.Interfaces;
using Daryva.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Daryva.Api.Services;

public class ExpenseService : IExpenseService
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<ExpenseService> _logger;
    private readonly ITenantContext _tenantContext;
    private readonly IAuditLogger _auditLogger;

    public ExpenseService(AppDbContext dbContext, ILogger<ExpenseService> logger, ITenantContext tenantContext, IAuditLogger auditLogger)
    {
        _dbContext = dbContext;
        _logger = logger;
        _tenantContext = tenantContext;
        _auditLogger = auditLogger;
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
            LogAudit(AuditEventTypes.ExpenseDeleted, expense.OrganizationId, nameof(Expense), expense.Id.ToString());
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private void LogAudit(string eventType, Guid organizationId, string targetType, string targetId)
    {
        if (!Guid.TryParse(_tenantContext.UserId, out var actorId))
            return;

        _auditLogger.Log(actorId, _tenantContext.CurrentRole ?? "Unknown", eventType,
            organizationId: organizationId, targetType: targetType, targetId: targetId,
            supportSessionId: _tenantContext.ActiveSupportSessionId);
    }
}
