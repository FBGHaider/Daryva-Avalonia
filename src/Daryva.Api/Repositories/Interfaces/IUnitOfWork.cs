namespace Daryva.Api.Repositories.Interfaces;

/// <summary>
/// Commits changes staged by repositories in a single transaction.
/// Keeps DbContext.SaveChangesAsync out of services.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
