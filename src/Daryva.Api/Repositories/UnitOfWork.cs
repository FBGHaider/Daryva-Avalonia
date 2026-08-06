using Daryva.Api.Data;
using Daryva.Api.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore.Storage;

namespace Daryva.Api.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _dbContext;

    public UnitOfWork(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => _dbContext.SaveChangesAsync(cancellationToken);

    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        => _dbContext.Database.BeginTransactionAsync(cancellationToken);
}
