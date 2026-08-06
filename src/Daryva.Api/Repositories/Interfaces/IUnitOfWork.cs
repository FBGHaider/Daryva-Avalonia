using Microsoft.EntityFrameworkCore.Storage;

namespace Daryva.Api.Repositories.Interfaces;

/// <summary>
/// Commits changes staged by repositories in a single transaction.
/// Keeps DbContext.SaveChangesAsync out of services.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// For the rare case where a single logical operation mixes a tracked-entity save with a
    /// change-tracker-bypassing bulk operation (e.g. ExecuteUpdateAsync) that must commit atomically
    /// with it. Most callers should just use SaveChangesAsync -- only reach for this when two separate
    /// persistence calls need to succeed or fail together.
    /// </summary>
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
}
