using Daryva.Api.Domain;

namespace Daryva.Api.Repositories.Interfaces;

public interface ISupportSessionRepository
{
    /// <summary>
    /// The active (unended, unexpired) session for this admin+org pair, if any.
    /// </summary>
    Task<SupportSession?> GetActiveSessionAsync(Guid adminUserId, Guid organizationId, CancellationToken cancellationToken = default);
}
