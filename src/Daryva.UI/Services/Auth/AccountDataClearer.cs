using Daryva.Services.AppReset;

namespace Daryva.Services.Auth;

/// <summary>
/// Clears session, org context, and local files on sign-out. Delegates to <see cref="IAppResetService"/> for full reset.
/// </summary>
public sealed class AccountDataClearer : IAccountDataClearer
{
    private readonly IAppResetService _appResetService;

    public AccountDataClearer(IAppResetService appResetService)
    {
        _appResetService = appResetService ?? throw new ArgumentNullException(nameof(appResetService));
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        return _appResetService.ResetToSignedOutAsync(cancellationToken);
    }
}
