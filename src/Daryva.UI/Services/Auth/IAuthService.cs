namespace Daryva.Services.Auth;

/// <summary>
/// App auth session lifecycle: sign-in against the local email/password API, token refresh,
/// sign-out. Owns StateChanged so navigation (MainViewModel) reacts uniformly regardless of
/// which screen triggered the change.
/// </summary>
public interface IAuthService
{
    Task<bool> HasValidSessionAsync(CancellationToken cancellationToken = default);
    Task<AuthSignInResult> SignInAsync(string email, string password, CancellationToken cancellationToken = default);

    /// <summary>Completes a sign-in that returned RequiresTwoFactor. Throws on an invalid/expired code or token.</summary>
    Task VerifyTwoFactorAsync(string challengeToken, string code, CancellationToken cancellationToken = default);
    Task SignOutAsync(CancellationToken cancellationToken = default);
    Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default);
    Task<bool> TryRefreshAsync(CancellationToken cancellationToken = default);
    event EventHandler<AuthStateChangedEventArgs>? StateChanged;
}
