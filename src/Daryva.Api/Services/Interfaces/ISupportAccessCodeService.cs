using Daryva.Api.Dtos;

namespace Daryva.Api.Services.Interfaces;

public interface ISupportAccessCodeService
{
    /// <summary>Generates a short-lived code for organizationId, on behalf of the real member userId.</summary>
    Task<SupportAccessCodeResponse> GenerateAsync(string userId, Guid organizationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Looks up the org for an unexpired, unresolved code and marks it resolved (single-use).
    /// Null if the code doesn't exist, is expired, or was already resolved.
    /// </summary>
    Task<ResolvedSupportAccessCodeResponse?> ResolveAsync(string adminUserId, string code, CancellationToken cancellationToken = default);
}
