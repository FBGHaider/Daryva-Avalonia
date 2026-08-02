using Daryva.Api.Domain;
using Daryva.Api.Repositories.Interfaces;
using Daryva.Api.Services.Interfaces;

namespace Daryva.Api.Services;

public class PlatformAdminBootstrapper : IPlatformAdminBootstrapper
{
    private readonly IAppUserRepository _appUserRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditLogger _auditLogger;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PlatformAdminBootstrapper> _logger;

    public PlatformAdminBootstrapper(
        IAppUserRepository appUserRepository,
        IUnitOfWork unitOfWork,
        IAuditLogger auditLogger,
        IConfiguration configuration,
        ILogger<PlatformAdminBootstrapper> logger)
    {
        _appUserRepository = appUserRepository;
        _unitOfWork = unitOfWork;
        _auditLogger = auditLogger;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task BootstrapAsync(CancellationToken cancellationToken = default)
    {
        var configuredEmails = _configuration["Admin:BootstrapEmails"];
        if (string.IsNullOrWhiteSpace(configuredEmails))
            return;

        var emails = configuredEmails
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(e => e.ToLowerInvariant())
            .Distinct();

        var changed = false;
        foreach (var email in emails)
        {
            var user = await _appUserRepository.GetByEmailAsync(email, cancellationToken);
            if (user == null)
            {
                _logger.LogWarning("Admin:BootstrapEmails contains {Email} but no matching AppUser exists yet.", email);
                continue;
            }

            if (user.IsPlatformAdmin)
                continue;

            user.IsPlatformAdmin = true;
            _auditLogger.Log(Guid.Empty, AuditActorRoles.System, AuditEventTypes.PlatformAdminGranted,
                targetType: nameof(AppUser), targetId: user.Id.ToString());
            _logger.LogInformation("Granted platform admin to {Email} via Admin:BootstrapEmails.", email);
            changed = true;
        }

        if (changed)
            await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
