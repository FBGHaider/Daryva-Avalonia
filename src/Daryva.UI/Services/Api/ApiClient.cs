using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Daryva.Services.Auth;
using Daryva.Services.OrgContext;

namespace Daryva.Services.Api;

/// <summary>
/// HTTP client wrapper for Daryva.Api backend.
/// Handles base URL, Authorization Bearer, X-Org-Id from IOrgContext, 401 refresh (via IAuthService), transient retry, and friendly errors.
/// X-Org-Id is always taken from OrgContext so it is absent when no org is selected (e.g. SignInView/SetupRequired).
/// </summary>
public class ApiClient : IApiClient
{
    private const int TransientRetryDelayMs = 500;

    private readonly HttpClient _httpClient;
    private readonly IOrgContext _orgContext;
    private readonly IConfigurationService _configuration;
    private readonly IAuthSessionService _authSession;
    private readonly IServiceProvider _serviceProvider;

    public Guid? CurrentOrgId => _orgContext.CurrentOrgId;
    public HttpClient HttpClient => _httpClient;

    // IAuthService is resolved lazily via IServiceProvider (not constructor-injected) because
    // IAuthService -> IAuthApiService -> IApiClient would otherwise be a circular DI graph
    // (ApiClient -> IAuthService -> ... -> ApiClient). Resolving at point of use, after the
    // container has finished constructing everything, breaks the cycle.
    public ApiClient(IConfigurationService configuration, IAuthSessionService authSession, IOrgContext orgContext, IServiceProvider serviceProvider)
    {
        _configuration = configuration;
        _authSession = authSession;
        _orgContext = orgContext ?? throw new ArgumentNullException(nameof(orgContext));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

        var baseAddress = configuration.GetValue("ApiBaseUrl") ?? "http://localhost:5000";

        var authHandler = new ApiAuthHandler(this, new Uri(baseAddress), _authSession, _serviceProvider);
        authHandler.InnerHandler = new HttpClientHandler();
        
        _httpClient = new HttpClient(authHandler)
        {
            BaseAddress = new Uri(baseAddress),
            Timeout = TimeSpan.FromSeconds(60)
        };

        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");

        ApplyAuthState();
    }

    public void SetCurrentOrgId(Guid orgId)
    {
        _ = _orgContext.SetCurrentOrgAsync(orgId);
    }

    public void ClearCurrentOrgId()
    {
        _orgContext.ClearCurrentOrgSelection();
    }

    public void ApplyAuthState()
    {
        _httpClient.DefaultRequestHeaders.Authorization = null;
        if (!string.IsNullOrWhiteSpace(_authSession.AccessToken))
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _authSession.AccessToken);
        }
    }

    private sealed class ApiAuthHandler : DelegatingHandler
    {
        private readonly ApiClient _owner;
        private readonly Uri _baseAddress;
        private readonly IAuthSessionService _authSession;
        private readonly IServiceProvider _serviceProvider;
        private readonly SemaphoreSlim _refreshLock = new(1, 1);

        public ApiAuthHandler(ApiClient owner, Uri baseAddress, IAuthSessionService authSession, IServiceProvider serviceProvider)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            _baseAddress = baseAddress;
            _authSession = authSession;
            _serviceProvider = serviceProvider;
        }

        private IAuthService? AuthService => _serviceProvider.GetService(typeof(IAuthService)) as IAuthService;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var isAuthEndpoint = request.RequestUri?.AbsolutePath.Contains("/api/auth/", StringComparison.OrdinalIgnoreCase) == true;

            if (!isAuthEndpoint)
            {
                await EnsureFreshTokenIfNeededAsync(cancellationToken);
                AttachAccessToken(request);
                AttachOrgId(request);
            }

            var response = await SendWithTransientRetryAsync(request, cancellationToken);
            if (response.StatusCode != HttpStatusCode.Unauthorized || isAuthEndpoint)
                return response;

            response.Dispose();

            // A 401 with no session at all (background polling while signed out, or while sitting on
            // a pre-auth screen like Forgot Password/Sign Up) is expected and routine -- there was
            // nothing to sign out of. Only a request that HAD a session which then failed to refresh
            // represents a genuine session death worth reacting to. Without this check, any stray
            // unauthenticated background call (notification polling, org-label refresh on every
            // navigation, etc.) would call SignOutAsync -> StateChanged(false) -> MainViewModel
            // forcibly navigating away from whatever pre-auth screen the user was actually on.
            var hadSession = _authSession.IsAuthenticated;
            if (!hadSession)
                return new HttpResponseMessage(HttpStatusCode.Unauthorized) { RequestMessage = request };

            var refreshed = await RefreshAccessTokenAsync(cancellationToken);
            if (!refreshed)
            {
                var authService = AuthService;
                if (authService != null)
                    await authService.SignOutAsync(cancellationToken).ConfigureAwait(false);
                return new HttpResponseMessage(HttpStatusCode.Unauthorized) { RequestMessage = request };
            }

            var retryRequest = await CloneRequestAsync(request, cancellationToken);
            AttachAccessToken(retryRequest);
            AttachOrgId(retryRequest);
            return await base.SendAsync(retryRequest, cancellationToken);
        }

        private void AttachOrgId(HttpRequestMessage request)
        {
            request.Headers.Remove("X-Org-Id");
            if (_owner.CurrentOrgId.HasValue)
                request.Headers.TryAddWithoutValidation("X-Org-Id", _owner.CurrentOrgId.Value.ToString());
        }

        private async Task<HttpResponseMessage> SendWithTransientRetryAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            try
            {
                return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (HttpRequestException) when (request.Content == null)
            {
                await Task.Delay(TransientRetryDelayMs, cancellationToken).ConfigureAwait(false);
                var retryRequest = await CloneRequestAsync(request, cancellationToken).ConfigureAwait(false);
                return await base.SendAsync(retryRequest, cancellationToken).ConfigureAwait(false);
            }
        }

        private async Task EnsureFreshTokenIfNeededAsync(CancellationToken cancellationToken)
        {
            var authService = AuthService;
            if (authService != null)
            {
                await authService.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
                return;
            }
            if (!_authSession.IsAuthenticated || !_authSession.AccessTokenExpiresAtUtc.HasValue)
                return;
            var remaining = _authSession.AccessTokenExpiresAtUtc.Value - DateTime.UtcNow;
            if (remaining > TimeSpan.FromMinutes(1))
                return;
            await RefreshAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        }

        private void AttachAccessToken(HttpRequestMessage request)
        {
            if (!string.IsNullOrWhiteSpace(_authSession.AccessToken))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _authSession.AccessToken);
            }
        }

        private async Task<bool> RefreshAccessTokenAsync(CancellationToken cancellationToken)
        {
            var authService = AuthService;
            if (authService != null)
                return await authService.TryRefreshAsync(cancellationToken).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(_authSession.RefreshToken))
                return false;

            await _refreshLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (_authSession.AccessTokenExpiresAtUtc.HasValue &&
                    _authSession.AccessTokenExpiresAtUtc.Value > DateTime.UtcNow.AddMinutes(1))
                    return true;

                using var refreshClient = new HttpClient
                {
                    BaseAddress = _baseAddress,
                    Timeout = TimeSpan.FromSeconds(15)
                };
                var refreshResponse = await refreshClient.PostAsJsonAsync(
                    "api/auth/refresh",
                    new { refreshToken = _authSession.RefreshToken },
                    cancellationToken).ConfigureAwait(false);

                if (!refreshResponse.IsSuccessStatusCode)
                {
                    _authSession.ClearSession();
                    return false;
                }
                var tokens = await refreshResponse.Content.ReadFromJsonAsync<AuthTokensDto>(cancellationToken: cancellationToken).ConfigureAwait(false);
                if (tokens == null)
                {
                    _authSession.ClearSession();
                    return false;
                }
                _authSession.SetSession(tokens.AccessToken, tokens.RefreshToken, tokens.AccessTokenExpiresAt, tokens.UserId, tokens.Email);
                return true;
            }
            finally
            {
                _refreshLock.Release();
            }
        }

        private static async Task<HttpRequestMessage> CloneRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var clone = new HttpRequestMessage(request.Method, request.RequestUri);

            foreach (var header in request.Headers)
            {
                clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            if (request.Content != null)
            {
                var ms = new MemoryStream();
                await request.Content.CopyToAsync(ms, cancellationToken);
                ms.Position = 0;
                var content = new StreamContent(ms);
                foreach (var header in request.Content.Headers)
                {
                    content.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }

                clone.Content = content;
            }

            return clone;
        }
    }
}
