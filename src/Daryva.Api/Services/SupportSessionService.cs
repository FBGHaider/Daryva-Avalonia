using Daryva.Api.Domain;
using Daryva.Api.Dtos;
using Daryva.Api.Repositories.Interfaces;
using Daryva.Api.Security;
using Daryva.Api.Services.Interfaces;

namespace Daryva.Api.Services;

public class SupportSessionService : ISupportSessionService
{
    private const int DefaultDurationMinutes = 60;
    private const int MinDurationMinutes = 5;
    private const int MaxDurationMinutes = 240;

    private readonly ISupportSessionRepository _supportSessionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditLogger _auditLogger;

    public SupportSessionService(
        ISupportSessionRepository supportSessionRepository,
        IUnitOfWork unitOfWork,
        IAuditLogger auditLogger)
    {
        _supportSessionRepository = supportSessionRepository;
        _unitOfWork = unitOfWork;
        _auditLogger = auditLogger;
    }

    public async Task<SupportSessionResponse> StartAsync(string adminUserId, StartSupportSessionRequest request, string? clientIp, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(adminUserId, out var adminGuid))
            throw new InvalidOperationException("Invalid admin user id.");

        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new ArgumentException("A reason is required to start a support session.", nameof(request.Reason));

        if (!await _supportSessionRepository.OrganizationExistsAsync(request.OrganizationId, cancellationToken))
            throw new ArgumentException("Organization not found.", nameof(request.OrganizationId));

        var existing = await _supportSessionRepository.GetActiveSessionAsync(adminGuid, request.OrganizationId, cancellationToken);
        if (existing != null)
            throw new InvalidOperationException("An active support session already exists for this organization.");

        var durationMinutes = Math.Clamp(request.DurationMinutes ?? DefaultDurationMinutes, MinDurationMinutes, MaxDurationMinutes);
        var now = DateTime.UtcNow;

        var session = new SupportSession
        {
            Id = Guid.NewGuid(),
            AdminUserId = adminGuid,
            OrganizationId = request.OrganizationId,
            Reason = request.Reason.Trim(),
            StartedAt = now,
            ExpiresAt = now.AddMinutes(durationMinutes)
        };

        _supportSessionRepository.Add(session);

        _auditLogger.Log(adminGuid, Roles.Admin, AuditEventTypes.SupportSessionStarted,
            organizationId: session.OrganizationId, targetType: nameof(SupportSession), targetId: session.Id.ToString(),
            metadata: new { session.Reason, durationMinutes }, supportSessionId: session.Id, ipAddress: clientIp);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToResponse(session);
    }

    public async Task<SupportSessionResponse?> EndAsync(string adminUserId, Guid sessionId, string? clientIp, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(adminUserId, out var adminGuid))
            throw new InvalidOperationException("Invalid admin user id.");

        var session = await _supportSessionRepository.GetByIdAsync(sessionId, cancellationToken);
        if (session == null)
            return null;

        if (session.EndedAt == null)
        {
            session.EndedAt = DateTime.UtcNow;
            session.EndedReason = SupportSessionEndedReasons.ManuallyEnded;

            _auditLogger.Log(adminGuid, Roles.Admin, AuditEventTypes.SupportSessionEnded,
                organizationId: session.OrganizationId, targetType: nameof(SupportSession), targetId: session.Id.ToString(),
                supportSessionId: session.Id, ipAddress: clientIp);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return ToResponse(session);
    }

    public async Task<IEnumerable<SupportSessionResponse>> ListAsync(Guid? organizationId, bool includeEnded, CancellationToken cancellationToken = default)
    {
        var sessions = await _supportSessionRepository.ListAsync(organizationId, includeEnded, cancellationToken);
        return sessions.Select(ToResponse);
    }

    private static SupportSessionResponse ToResponse(SupportSession session)
    {
        var now = DateTime.UtcNow;
        return new SupportSessionResponse
        {
            Id = session.Id,
            AdminUserId = session.AdminUserId,
            OrganizationId = session.OrganizationId,
            Reason = session.Reason,
            StartedAt = session.StartedAt,
            ExpiresAt = session.ExpiresAt,
            EndedAt = session.EndedAt,
            EndedReason = session.EndedReason,
            IsActive = session.EndedAt == null && session.ExpiresAt > now
        };
    }
}
