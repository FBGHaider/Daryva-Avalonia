namespace Daryva.Api.Services.Interfaces;

/// <summary>
/// Idempotently grants IsPlatformAdmin to the emails listed in Admin:BootstrapEmails on every
/// startup. Only ever adds the flag -- never removes it, so clearing the config doesn't demote
/// anyone. See Docs/platform-admin-bootstrap.md.
/// </summary>
public interface IPlatformAdminBootstrapper
{
    Task BootstrapAsync(CancellationToken cancellationToken = default);
}
