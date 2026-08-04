using Daryva.Api.Domain;

namespace Daryva.Api.Repositories.Interfaces;

public interface ISupportAccessCodeRepository
{
    /// <summary>Case-insensitive exact match. Null if no such code exists at all (expired/resolved
    /// codes are still returned -- ResolveAsync in the service layer decides validity).</summary>
    Task<SupportAccessCode?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>Null if no such organization -- doubles as the existence check.</summary>
    Task<string?> GetOrganizationNameAsync(Guid organizationId, CancellationToken cancellationToken = default);

    void Add(SupportAccessCode accessCode);
}
