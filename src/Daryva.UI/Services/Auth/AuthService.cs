using Daryva.Services.Api;
using Daryva.Services.AppReset;
using Daryva.Services.Session;
using Microsoft.Extensions.DependencyInjection;

namespace Daryva.Services.Auth;

/// <summary>
/// Local email/password auth against Daryva.Api. Session state lives in IAuthSessionService
/// (read by ApiClient for outgoing requests); this class only coordinates the sign-in/refresh/
/// sign-out lifecycle and raises StateChanged so MainViewModel can react uniformly.
/// </summary>
public sealed class AuthService : IAuthService
{
    private readonly IAuthApiService _authApiService;
    private readonly IAuthSessionService _authSession;
    private readonly ISessionContext _sessionContext;
    private readonly IServiceProvider _serviceProvider;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    public event EventHandler<AuthStateChangedEventArgs>? StateChanged;

    public AuthService(
        IAuthApiService authApiService,
        IAuthSessionService authSession,
        ISessionContext sessionContext,
        IServiceProvider serviceProvider)
    {
        _authApiService = authApiService;
        _authSession = authSession;
        _sessionContext = sessionContext ?? throw new ArgumentNullException(nameof(sessionContext));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    public Task<bool> HasValidSessionAsync(CancellationToken cancellationToken = default)
    {
        if (!_authSession.IsAuthenticated || string.IsNullOrWhiteSpace(_authSession.AccessToken))
            return Task.FromResult(false);
        if (_authSession.AccessTokenExpiresAtUtc is { } expiresAtUtc && expiresAtUtc <= DateTime.UtcNow)
            return TryRefreshAsync(cancellationToken);
        return Task.FromResult(true);
    }

    public async Task<AuthSignInResult> SignInAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        var result = await _authApiService.LoginAsync(email, password, cancellationToken).ConfigureAwait(false);
        if (result.RequiresTwoFactor)
            return new AuthSignInResult { RequiresTwoFactor = true, ChallengeToken = result.ChallengeToken };

        _sessionContext.SetFromToken(result.UserId ?? string.Empty, result.Email ?? string.Empty);
        RaiseStateChanged(true);
        return new AuthSignInResult { RequiresTwoFactor = false };
    }

    public async Task VerifyTwoFactorAsync(string challengeToken, string code, CancellationToken cancellationToken = default)
    {
        var tokens = await _authApiService.VerifyTwoFactorAsync(challengeToken, code, cancellationToken).ConfigureAwait(false);
        _sessionContext.SetFromToken(tokens.UserId, tokens.Email);
        RaiseStateChanged(true);
    }

    public async Task SignOutAsync(CancellationToken cancellationToken = default)
    {
        // No-op if already signed out (defense in depth alongside ApiAuthHandler's own
        // hadSession check) -- there's nothing to sign out of, and raising StateChanged here
        // would forcibly navigate the user away from wherever they currently are, including
        // pre-auth screens like Forgot Password/Sign Up.
        if (!_authSession.IsAuthenticated)
            return;

        await _authApiService.LogoutAsync(cancellationToken).ConfigureAwait(false);
        var resetService = _serviceProvider.GetRequiredService<IAppResetService>();
        await resetService.ResetToSignedOutAsync(cancellationToken).ConfigureAwait(false);
        RaiseStateChanged(false);
    }

    public async Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        if (!_authSession.IsAuthenticated)
            return null;
        if (_authSession.AccessTokenExpiresAtUtc is { } expiresAtUtc && expiresAtUtc <= DateTime.UtcNow)
        {
            var refreshed = await TryRefreshAsync(cancellationToken).ConfigureAwait(false);
            if (!refreshed)
                return null;
        }
        return _authSession.AccessToken;
    }

    public async Task<bool> TryRefreshAsync(CancellationToken cancellationToken = default)
    {
        await _refreshLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_authSession.AccessTokenExpiresAtUtc is { } expiresAtUtc && expiresAtUtc > DateTime.UtcNow.AddMinutes(1))
                return true;

            var refreshToken = _authSession.RefreshToken;
            if (string.IsNullOrWhiteSpace(refreshToken))
                return false;

            try
            {
                var tokens = await _authApiService.RefreshAsync(refreshToken, cancellationToken).ConfigureAwait(false);
                _authSession.SetSession(tokens.AccessToken, tokens.RefreshToken, tokens.AccessTokenExpiresAt, tokens.UserId, tokens.Email);
                return true;
            }
            catch
            {
                _authSession.ClearSession();
                RaiseStateChanged(false);
                return false;
            }
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private void RaiseStateChanged(bool isSignedIn)
    {
        StateChanged?.Invoke(this, new AuthStateChangedEventArgs { IsSignedIn = isSignedIn });
    }
}
