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
    private readonly IOrganizationMemberRepository _organizationMemberRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditLogger _auditLogger;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<SupportSessionService> _logger;

    public SupportSessionService(
        ISupportSessionRepository supportSessionRepository,
        IOrganizationMemberRepository organizationMemberRepository,
        IUnitOfWork unitOfWork,
        IAuditLogger auditLogger,
        IEmailSender emailSender,
        ILogger<SupportSessionService> logger)
    {
        _supportSessionRepository = supportSessionRepository;
        _organizationMemberRepository = organizationMemberRepository;
        _unitOfWork = unitOfWork;
        _auditLogger = auditLogger;
        _emailSender = emailSender;
        _logger = logger;
    }

    public async Task<SupportSessionResponse> StartAsync(string adminUserId, StartSupportSessionRequest request, string? clientIp, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(adminUserId, out var adminGuid))
            throw new InvalidOperationException("Invalid admin user id.");

        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new ArgumentException("A reason is required to start a support session.", nameof(request.Reason));

        var organizationName = await _supportSessionRepository.GetOrganizationNameAsync(request.OrganizationId, cancellationToken);
        if (organizationName == null)
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

        await NotifyLandlordsAsync(session, organizationName, cancellationToken);

        return ToResponse(session);
    }

    /// <summary>
    /// Best-effort transactional email, not staged through IAuditLogger/IUnitOfWork -- the session
    /// is already committed by the time this runs, so a failed send must not fail the request or
    /// roll back the (already-persisted) session start.
    /// </summary>
    private async Task NotifyLandlordsAsync(SupportSession session, string organizationName, CancellationToken cancellationToken)
    {
        var members = await _organizationMemberRepository.GetByOrganizationIdAsync(session.OrganizationId, cancellationToken);
        var recipients = members
            .Where(m => m.Role == Domain.OrganizationMember.Roles.Landlord && !string.IsNullOrWhiteSpace(m.Email))
            .Select(m => m.Email!)
            .Distinct();

        var subject = $"Daryva support access started on {organizationName}";
        var body = $"A Daryva platform administrator has started a support session on your organization \"{organizationName}\".\n\n" +
            $"Reason given: {session.Reason}\n" +
            $"Started: {session.StartedAt:u}\n" +
            $"Expires: {session.ExpiresAt:u} (or when manually ended)\n\n" +
            "While active, the administrator can view and manage your organization's data to help resolve your issue. " +
            "Every action they take is recorded in your organization's audit log.";

        foreach (var email in recipients)
        {
            try
            {
                await _emailSender.SendEmailAsync(email, subject, body);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send support-session-started notification to {Email}", email);
            }
        }
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
