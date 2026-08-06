using Daryva.Api.Domain;
using Daryva.Api.Repositories.Interfaces;
using Daryva.Api.Security.Interfaces;
using Daryva.Api.Services.Interfaces;

namespace Daryva.Api.Services;

public class ExpenseService : IExpenseService
{
    private readonly IExpenseRepository _expenseRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ExpenseService> _logger;
    private readonly ITenantContext _tenantContext;
    private readonly IAuditLogger _auditLogger;

    public ExpenseService(
        IExpenseRepository expenseRepository,
        IUnitOfWork unitOfWork,
        ILogger<ExpenseService> logger,
        ITenantContext tenantContext,
        IAuditLogger auditLogger)
    {
        _expenseRepository = expenseRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _tenantContext = tenantContext;
        _auditLogger = auditLogger;
    }

    public async Task<IEnumerable<Expense>> GetAllExpensesAsync(bool includeArchived = false, CancellationToken cancellationToken = default)
        => await _expenseRepository.GetAllAsync(includeArchived, cancellationToken);

    public async Task<IEnumerable<Expense>> GetExpensesByHouseAsync(Guid houseId, bool includeArchived = false, CancellationToken cancellationToken = default)
        => await _expenseRepository.GetByHouseIdAsync(houseId, includeArchived, cancellationToken);

    public async Task<Expense?> GetExpenseByIdAsync(Guid expenseId, CancellationToken cancellationToken = default)
        => await _expenseRepository.GetByIdAsync(expenseId, cancellationToken);

    public async Task<Expense> CreateExpenseAsync(Expense expense, CancellationToken cancellationToken = default)
    {
        _expenseRepository.Add(expense);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return expense;
    }

    public async Task UpdateExpenseAsync(Expense expense, CancellationToken cancellationToken = default)
    {
        _expenseRepository.Update(expense);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task ArchiveExpenseAsync(Guid expenseId, CancellationToken cancellationToken = default)
    {
        var expense = await _expenseRepository.GetTrackedByIdAsync(expenseId, cancellationToken);
        if (expense != null)
        {
            expense.IsArchived = true;
            LogAudit(AuditEventTypes.ExpenseArchived, expense.OrganizationId, nameof(Expense), expense.Id.ToString());
            await _unitOfWork.SaveChangesAsync(cancellationToken);
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
