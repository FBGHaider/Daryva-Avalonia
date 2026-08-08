using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Daryva.Services;
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

        // Every request from every ViewModel funnels through this single handler, so it's the one
        // place that can cap how many requests are ever ACTIVELY ON THE WIRE at once app-wide --
        // individual ViewModels are free to fire off several independent calls concurrently (e.g.
        // the dashboard's Task.WhenAll load) without knowing or caring about any other ViewModel's
        // simultaneous bursts. This gate is acquired only around the actual base.SendAsync calls
        // (see SendGatedAsync) -- NOT around the retry/backoff loop below. Holding it across a
        // multi-second 429 backoff sleep would let a single throttled request tie up one of a
        // handful of global slots for 20+ seconds, and with only a few slots total, every other
        // page's requests (e.g. from rapid tab switching) would queue up behind it -- which is
        // exactly the "everything grinds to a halt" regression an earlier version of this gate caused.
        private static readonly SemaphoreSlim _concurrencyGate = new(6, 6);
        private const int MaxRateLimitRetries = 4;
        private static readonly TimeSpan RateLimitRetryDelay = TimeSpan.FromSeconds(1);

        public ApiAuthHandler(ApiClient owner, Uri baseAddress, IAuthSessionService authSession, IServiceProvider serviceProvider)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            _baseAddress = baseAddress;
            _authSession = authSession;
            _serviceProvider = serviceProvider;
        }

        private IAuthService? AuthService => _serviceProvider.GetService(typeof(IAuthService)) as IAuthService;

        private async Task<HttpResponseMessage> SendGatedAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.PathAndQuery ?? request.RequestUri?.ToString() ?? "(unknown)";
            var waitSw = Stopwatch.StartNew();
            await _concurrencyGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            waitSw.Stop();

            var callSw = Stopwatch.StartNew();
            try
            {
                var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
                callSw.Stop();
                AppLogger.Log("Http", $"{request.Method} {path} -> {(int)response.StatusCode} " +
                    $"({callSw.ElapsedMilliseconds}ms, queued {waitSw.ElapsedMilliseconds}ms)");
                return response;
            }
            catch (Exception ex)
            {
                AppLogger.LogError("Http", $"{request.Method} {path} threw {ex.GetType().Name}: {ex.Message} " +
                    $"({callSw.ElapsedMilliseconds}ms, queued {waitSw.ElapsedMilliseconds}ms)");
                throw;
            }
            finally
            {
                _concurrencyGate.Release();
            }
        }

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
            return await SendGatedAsync(retryRequest, cancellationToken);
        }

        private void AttachOrgId(HttpRequestMessage request)
        {
            request.Headers.Remove("X-Org-Id");
            if (_owner.CurrentOrgId.HasValue)
                request.Headers.TryAddWithoutValidation("X-Org-Id", _owner.CurrentOrgId.Value.ToString());
        }

        private async Task<HttpResponseMessage> SendWithTransientRetryAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // Both retry paths below must only ever apply to GET requests. A POST/PUT/DELETE isn't
            // guaranteed idempotent just because it has no request body -- api/auth/2fa/enroll is a
            // bodyless POST that generates and persists a brand-new TOTP secret on every single
            // call, overwriting whatever secret a previous call just returned. The old guard here
            // was "when (request.Content == null)", which let this exact endpoint get silently
            // retried on a transient network blip or a 429 (the 429 loop below previously had no
            // guard at all) -- each retry replaced the pending secret server-side and pushed a new
            // QR code to the UI while the user might already be mid-scan of the previous one,
            // producing multiple different secrets in their authenticator app from what looked like
            // one scan. The same risk applies to every other non-GET endpoint in the app (recording
            // a payment, archiving a tenant, etc.), not just this one -- restricting retries to GET
            // closes that whole class of "duplicate write from an automatic retry" bug at once.
            var isRetryable = request.Method == HttpMethod.Get;

            HttpResponseMessage response;
            try
            {
                response = await SendGatedAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (HttpRequestException) when (isRetryable)
            {
                await Task.Delay(TransientRetryDelayMs, cancellationToken).ConfigureAwait(false);
                var retryRequest = await CloneRequestAsync(request, cancellationToken).ConfigureAwait(false);
                return await SendGatedAsync(retryRequest, cancellationToken).ConfigureAwait(false);
            }

            // Back off and retry a few times instead of surfacing a raw 429 dialog for what's
            // usually a transient "wait a second" condition. The sleep below happens OUTSIDE
            // SendGatedAsync's semaphore hold (each retry attempt re-acquires it fresh), so a
            // throttled request backing off doesn't block other pages' unrelated requests from
            // using that concurrency slot in the meantime.
            var retryCount = 0;
            while (isRetryable && response.StatusCode == HttpStatusCode.TooManyRequests && retryCount < MaxRateLimitRetries)
            {
                var delay = response.Headers.RetryAfter?.Delta ?? RateLimitRetryDelay * (retryCount + 1);
                AppLogger.Log("Http", $"429 on {request.Method} {request.RequestUri?.PathAndQuery} -- " +
                    $"retry {retryCount + 1}/{MaxRateLimitRetries} after {delay.TotalMilliseconds:F0}ms");
                response.Dispose();
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);

                var retryRequest = await CloneRequestAsync(request, cancellationToken).ConfigureAwait(false);
                response = await SendGatedAsync(retryRequest, cancellationToken).ConfigureAwait(false);
                retryCount++;
            }

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
                AppLogger.LogError("Http", $"429 on {request.Method} {request.RequestUri?.PathAndQuery} -- " +
                    $"gave up after {retryCount} retries");

            return response;
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
