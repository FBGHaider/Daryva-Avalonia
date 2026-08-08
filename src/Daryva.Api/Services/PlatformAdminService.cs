using Daryva.Api.Domain;
using Daryva.Api.Dtos;
using Daryva.Api.Repositories.Interfaces;
using Daryva.Api.Security;
using Daryva.Api.Services.Interfaces;

namespace Daryva.Api.Services;

public class PlatformAdminService : IPlatformAdminService
{
    private readonly IAppUserRepository _appUserRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditLogger _auditLogger;

    public PlatformAdminService(IAppUserRepository appUserRepository, IUnitOfWork unitOfWork, IAuditLogger auditLogger)
    {
        _appUserRepository = appUserRepository;
        _unitOfWork = unitOfWork;
        _auditLogger = auditLogger;
    }

    public async Task<IEnumerable<PlatformAdminResponse>> ListAsync(CancellationToken cancellationToken = default)
    {
        var admins = await _appUserRepository.ListPlatformAdminsAsync(cancellationToken);
        return admins.Select(ToResponse);
    }

    public async Task RevokeAsync(string actorUserId, Guid targetUserId, string? clientIp, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(actorUserId, out var actorGuid))
            throw new InvalidOperationException("Invalid admin user id.");

        // Granting admin only happens via server config (Admin:BootstrapEmails) + a restart -- there
        // is no in-app "make me admin again" path. Letting an admin revoke their own access would be
        // a one-way, self-inflicted lockout, so this is blocked outright rather than just discouraged.
        if (targetUserId == actorGuid)
            throw new ArgumentException("You cannot revoke your own admin access.", nameof(targetUserId));

        var target = await _appUserRepository.GetByIdAsync(targetUserId, cancellationToken)
            ?? throw new ArgumentException("User not found.", nameof(targetUserId));

        // Idempotent, matching SupportSessionService.EndAsync's pattern: revoking someone who's
        // already not an admin (e.g. a second click, or the list was stale) is a silent no-op
        // rather than an error -- the caller's desired end state is already true.
        if (!target.IsPlatformAdmin)
            return;

        target.IsPlatformAdmin = false;

        _auditLogger.Log(actorGuid, Roles.Admin, AuditEventTypes.PlatformAdminRevoked,
            targetType: nameof(AppUser), targetId: target.Id.ToString(), ipAddress: clientIp);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static PlatformAdminResponse ToResponse(AppUser user) => new()
    {
        Id = user.Id,
        Email = user.Email,
        FirstName = user.FirstName,
        LastName = user.LastName,
        CreatedAt = user.CreatedAt
    };
}
