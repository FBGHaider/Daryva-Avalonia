using Daryva.Api.Data;
using Daryva.Api.Domain;
using Daryva.Api.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Daryva.Api.Repositories;

public class ExpenseRepository : OrgScopedRepository<Expense>, IExpenseRepository
{
    public ExpenseRepository(AppDbContext dbContext) : base(dbContext)
    {
    }

    public Task<List<Expense>> GetAllAsync(bool includeArchived, CancellationToken cancellationToken = default)
    {
        var query = Set.Include(e => e.House).AsNoTracking().AsQueryable();
        if (!includeArchived)
            query = query.Where(e => !e.IsArchived);
        return query.ToListAsync(cancellationToken);
    }

    public Task<List<Expense>> GetByHouseIdAsync(Guid houseId, bool includeArchived, CancellationToken cancellationToken = default)
    {
        var query = Set.Include(e => e.House).AsNoTracking().Where(e => e.HouseId == houseId);
        if (!includeArchived)
            query = query.Where(e => !e.IsArchived);
        return query.ToListAsync(cancellationToken);
    }

    public Task<Expense?> GetByIdAsync(Guid expenseId, CancellationToken cancellationToken = default)
        => Set.Include(e => e.House).AsNoTracking().FirstOrDefaultAsync(e => e.Id == expenseId, cancellationToken);

    public Task<Expense?> GetTrackedByIdAsync(Guid expenseId, CancellationToken cancellationToken = default)
        => Set.FirstOrDefaultAsync(e => e.Id == expenseId, cancellationToken);
}
