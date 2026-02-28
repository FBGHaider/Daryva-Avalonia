using System.Security.Claims;
using Daryva.Services.Api;
using Duende.IdentityModel.OidcClient;
using Duende.IdentityModel.OidcClient.Browser;

namespace Daryva.Services.Auth;

/// <summary>
/// SaaS auth via OIDC Authorization Code + PKCE; system browser and loopback redirect.
/// Persists tokens with <see cref="ITokenStore"/>; syncs to <see cref="IAuthSessionService"/> for existing ApiClient.
/// </summary>
public sealed class AuthService : IAuthService
{
    private readonly ITokenStore _tokenStore;
    private readonly IAuthSessionService _authSession;
    private readonly IConfigurationService _configuration;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private OidcClient? _oidcClient;

    public event EventHandler<AuthStateChangedEventArgs>? StateChanged;

    public AuthService(
        ITokenStore tokenStore,
        IAuthSessionService authSession,
        IConfigurationService configuration)
    {
        _tokenStore = tokenStore;
        _authSession = authSession;
        _configuration = configuration;
    }

    public async Task<bool> HasValidSessionAsync(CancellationToken cancellationToken = default)
    {
        var token = await _tokenStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        if (token == null || string.IsNullOrWhiteSpace(token.AccessToken))
            return false;
        if (token.ExpiresAtUtc <= DateTime.UtcNow)
            return await TryRefreshAsync(cancellationToken).ConfigureAwait(false);
        SyncToSessionIfNeeded(token);
        return true;
    }

    public async Task SignInAsync(CancellationToken cancellationToken = default)
    {
        var client = GetOrCreateOidcClient();
        var result = await client.LoginAsync(new LoginRequest(), cancellationToken).ConfigureAwait(false);
        if (result.IsError)
        {
            await _tokenStore.ClearAsync(cancellationToken).ConfigureAwait(false);
            _authSession.ClearSession();
            RaiseStateChanged(false);
            var msg = result.Error ?? "Sign-in failed.";
            if (!string.IsNullOrWhiteSpace(result.ErrorDescription))
                msg += " " + result.ErrorDescription;
            throw new InvalidOperationException(msg);
        }

        var expiresAtUtc = result.AccessTokenExpiration.UtcDateTime;

        var userId = result.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? result.User?.FindFirst("sub")?.Value ?? string.Empty;
        var email = result.User?.FindFirst(ClaimTypes.Email)?.Value ?? result.User?.FindFirst("email")?.Value ?? string.Empty;
        var stored = new StoredToken
        {
            AccessToken = result.AccessToken ?? string.Empty,
            RefreshToken = result.RefreshToken,
            ExpiresAtUtc = expiresAtUtc,
            UserId = userId,
            Email = email
        };
        await _tokenStore.SaveAsync(stored, cancellationToken).ConfigureAwait(false);
        _authSession.SetSession(result.AccessToken ?? string.Empty, result.RefreshToken ?? string.Empty, expiresAtUtc, userId, email);
        RaiseStateChanged(true);
    }

    public async Task SignOutAsync(CancellationToken cancellationToken = default)
    {
        await _tokenStore.ClearAsync(cancellationToken).ConfigureAwait(false);
        _authSession.ClearSession();
        RaiseStateChanged(false);
    }

    public async Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        var token = await _tokenStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        if (token == null || string.IsNullOrWhiteSpace(token.AccessToken))
            return null;
        if (token.ExpiresAtUtc <= DateTime.UtcNow)
        {
            var refreshed = await TryRefreshAsync(cancellationToken).ConfigureAwait(false);
            if (!refreshed)
                return null;
            token = await _tokenStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        }
        SyncToSessionIfNeeded(token);
        return token?.AccessToken;
    }

    public async Task<bool> TryRefreshAsync(CancellationToken cancellationToken = default)
    {
        await _refreshLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var token = await _tokenStore.LoadAsync(cancellationToken).ConfigureAwait(false);
            if (token == null || string.IsNullOrWhiteSpace(token.RefreshToken))
                return false;
            if (token.ExpiresAtUtc > DateTime.UtcNow.AddMinutes(1))
                return true;

            var client = GetOrCreateOidcClient();
            var result = await client.RefreshTokenAsync(token.RefreshToken, default, null, cancellationToken).ConfigureAwait(false);
            if (result.IsError || string.IsNullOrWhiteSpace(result.AccessToken))
            {
                await _tokenStore.ClearAsync(cancellationToken).ConfigureAwait(false);
                _authSession.ClearSession();
                RaiseStateChanged(false);
                return false;
            }

            var expiresAtUtc = result.AccessTokenExpiration.UtcDateTime;

            var stored = new StoredToken
            {
                AccessToken = result.AccessToken,
                RefreshToken = result.RefreshToken ?? token.RefreshToken,
                ExpiresAtUtc = expiresAtUtc,
                UserId = _authSession.UserId,
                Email = _authSession.Email
            };
            await _tokenStore.SaveAsync(stored, cancellationToken).ConfigureAwait(false);
            _authSession.UpdateAccessToken(result.AccessToken, expiresAtUtc);
            if (!string.IsNullOrWhiteSpace(result.RefreshToken))
                _authSession.SetSession(result.AccessToken, result.RefreshToken, expiresAtUtc, _authSession.UserId ?? string.Empty, _authSession.Email ?? string.Empty);
            return true;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private OidcClient GetOrCreateOidcClient()
    {
        if (_oidcClient != null)
            return _oidcClient;

        var authority = _configuration.GetValue("Oidc:Authority")?.Trim();
        var clientId = _configuration.GetValue("Oidc:ClientId")?.Trim();
        if (string.IsNullOrEmpty(authority) || string.IsNullOrEmpty(clientId))
            throw new InvalidOperationException("Oidc:Authority and Oidc:ClientId must be set in configuration.");

        var redirectUri = $"http://127.0.0.1:{LoopbackBrowser.LoopbackPort}/callback";
        var options = new OidcClientOptions
        {
            Authority = authority.TrimEnd('/') + "/",
            ClientId = clientId,
            ClientSecret = null, // Public client (PKCE only)
            RedirectUri = redirectUri,
            Scope = "openid profile email offline_access",
            Browser = new LoopbackBrowser(),
            // Clerk public clients must use auth method "none": no Authorization header, no client_secret in body.
            BackchannelHandler = new ClerkNoSecretBackchannelHandler()
        };
        _oidcClient = new OidcClient(options);
        return _oidcClient;
    }

    private void SyncToSessionIfNeeded(StoredToken? token)
    {
        if (token == null || string.IsNullOrWhiteSpace(token.AccessToken))
            return;
        if (_authSession.AccessToken == token.AccessToken)
            return;
        _authSession.SetSession(
            token.AccessToken,
            token.RefreshToken ?? string.Empty,
            token.ExpiresAtUtc,
            token.UserId ?? string.Empty,
            token.Email ?? string.Empty);
    }

    private void RaiseStateChanged(bool isSignedIn)
    {
        StateChanged?.Invoke(this, new AuthStateChangedEventArgs { IsSignedIn = isSignedIn });
    }
}
