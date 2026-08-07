using Daryva.Api.Repositories.Interfaces;
using Daryva.Api.Security;
using Daryva.Api.Security.Interfaces;
using Daryva.Api.Services.Interfaces;

namespace Daryva.Api.Services;

public class TenantInviteService : ITenantInviteService
{
    private static readonly TimeSpan InviteTokenLifetime = TimeSpan.FromHours(72);

    private readonly ITenantRepository _tenantRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEmailSender _emailSender;
    private readonly IConfiguration _configuration;
    private readonly ITenantContext _tenantContext;
    private readonly IAuditLogger _auditLogger;
    private readonly ILogger<TenantInviteService> _logger;

    public TenantInviteService(
        ITenantRepository tenantRepository,
        IUnitOfWork unitOfWork,
        IEmailSender emailSender,
        IConfiguration configuration,
        ITenantContext tenantContext,
        IAuditLogger auditLogger,
        ILogger<TenantInviteService> logger)
    {
        _tenantRepository = tenantRepository;
        _unitOfWork = unitOfWork;
        _emailSender = emailSender;
        _configuration = configuration;
        _tenantContext = tenantContext;
        _auditLogger = auditLogger;
        _logger = logger;
    }

    public async Task SendInviteAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        // Org-scoped query filter already ensures this can't return a different org's tenant.
        var tenant = await _tenantRepository.GetTrackedByIdAsync(tenantId, cancellationToken);
        if (tenant == null)
            throw new KeyNotFoundException("Tenant not found.");

        if (string.IsNullOrWhiteSpace(tenant.Email))
            throw new ArgumentException("Tenant does not have an email address on file.");

        var now = DateTime.UtcNow;
        var tokenPlain = TokenGenerator.GenerateToken();
        tenant.InviteTokenHash = TokenGenerator.Hash(tokenPlain);
        tenant.InviteTokenExpiresAt = now.Add(InviteTokenLifetime);
        tenant.InviteSentAt = now;

        if (Guid.TryParse(_tenantContext.UserId, out var actorId))
        {
            _auditLogger.Log(actorId, _tenantContext.CurrentRole ?? "Unknown", AuditEventTypes.TenantInviteSent,
                organizationId: tenant.OrganizationId, targetType: nameof(Domain.Tenant), targetId: tenant.Id.ToString(),
                supportSessionId: _tenantContext.ActiveSupportSessionId);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await TrySendInviteEmailAsync(tenant.Email, tenant.FullName, tokenPlain, cancellationToken);
    }

    private async Task TrySendInviteEmailAsync(string email, string tenantName, string token, CancellationToken cancellationToken)
    {
        var acceptUrl = BuildAcceptInviteUrl(token);
        var subject = "You're invited to your Daryva tenant portal";
        var body = $"Hi {tenantName},\n\n" +
            $"Your landlord has invited you to view your tenancy documents online. " +
            $"Set up your portal login here:\n{acceptUrl}\n\n" +
            $"This link expires in 72 hours. If you weren't expecting this, you can ignore this email.";

        try
        {
            await _emailSender.SendEmailAsync(email, subject, body);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send tenant invite email to {Email}", email);
        }
    }

    private string BuildAcceptInviteUrl(string token)
    {
        var baseUrl = _configuration["Portal:AcceptInviteUrlBase"];
        if (string.IsNullOrWhiteSpace(baseUrl))
            baseUrl = "http://localhost:4322/accept-invite";

        var separator = baseUrl.Contains('?') ? "&" : "?";
        return $"{baseUrl}{separator}token={Uri.EscapeDataString(token)}";
    }
}
