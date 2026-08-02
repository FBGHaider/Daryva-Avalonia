using Daryva.Api.Dtos;

namespace Daryva.Api.Services.Interfaces;

public interface ISupportSessionService
{
    Task<SupportSessionResponse> StartAsync(string adminUserId, StartSupportSessionRequest request, string? clientIp, CancellationToken cancellationToken = default);

    /// <summary>Ends an active session early. Returns null if no session exists with this id.</summary>
    Task<SupportSessionResponse?> EndAsync(string adminUserId, Guid sessionId, string? clientIp, CancellationToken cancellationToken = default);

    Task<IEnumerable<SupportSessionResponse>> ListAsync(Guid? organizationId, bool includeEnded, CancellationToken cancellationToken = default);
}
