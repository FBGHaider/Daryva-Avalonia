using Daryva.Api.Domain;

namespace Daryva.Api.Repositories.Interfaces;

public interface ISupportSessionRepository
{
    /// <summary>
    /// The active (unended, unexpired) session for this admin+org pair, if any.
    /// </summary>
    Task<SupportSession?> GetActiveSessionAsync(Guid adminUserId, Guid organizationId, CancellationToken cancellationToken = default);

    Task<SupportSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Optionally filtered by org; excludes ended sessions unless includeEnded is true. Expired-but-unended sessions are included (EndedAt is only set by an explicit end call).</summary>
    Task<List<SupportSession>> ListAsync(Guid? organizationId, bool includeEnded, CancellationToken cancellationToken = default);

    /// <summary>Null if no such organization -- doubles as the existence check.</summary>
    Task<string?> GetOrganizationNameAsync(Guid organizationId, CancellationToken cancellationToken = default);

    /// <summary>Batch lookup for enriching a session list without one query per row.</summary>
    Task<Dictionary<Guid, string>> GetOrganizationNamesAsync(IEnumerable<Guid> organizationIds, CancellationToken cancellationToken = default);

    void Add(SupportSession session);
}
