namespace Daryva.Services.Auth;

/// <summary>
/// SaaS authentication: OIDC sign-in, token access, refresh, sign-out.
/// </summary>
public interface IAuthService
{
    Task<bool> HasValidSessionAsync(CancellationToken cancellationToken = default);
    Task SignInAsync(CancellationToken cancellationToken = default);
    Task SignOutAsync(CancellationToken cancellationToken = default);
    Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default);
    Task<bool> TryRefreshAsync(CancellationToken cancellationToken = default);
    event EventHandler<AuthStateChangedEventArgs>? StateChanged;
}
