using Daryva.Api.Dtos;

namespace Daryva.Api.Services.Interfaces;

public interface IPlatformAdminService
{
    Task<IEnumerable<PlatformAdminResponse>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Revokes AppUser.IsPlatformAdmin for the target user. Throws ArgumentException if the
    /// target doesn't exist or the caller is trying to revoke their own admin access (self-revoke
    /// would permanently lock an admin out, since the only grant path is server config + restart).
    /// Idempotent: revoking a user who is already not an admin is a no-op, not an error.</summary>
    Task RevokeAsync(string actorUserId, Guid targetUserId, string? clientIp, CancellationToken cancellationToken = default);
}
