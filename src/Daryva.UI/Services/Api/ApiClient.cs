using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Daryva.Services.Api;

/// <summary>
/// HTTP client wrapper for Daryva.Api backend.
/// Handles base URL configuration and X-Org-Id header management.
/// </summary>
public class ApiClient : IApiClient
{
    private const string ApiCurrentOrgIdKey = "ApiCurrentOrgId";

    private readonly HttpClient _httpClient;
    private Guid? _currentOrgId;
    private readonly IConfigurationService _configuration;
    private readonly IAuthSessionService _authSession;

    public Guid? CurrentOrgId => _currentOrgId;
    public HttpClient HttpClient => _httpClient;

    public ApiClient(IConfigurationService configuration, IAuthSessionService authSession)
    {
        _configuration = configuration;
        _authSession = authSession;

        var baseAddress = configuration.GetValue("ApiBaseUrl") ?? "http://localhost:5000";

        var authHandler = new ApiAuthHandler(new Uri(baseAddress), _authSession);
        authHandler.InnerHandler = new HttpClientHandler();
        
        _httpClient = new HttpClient(authHandler)
        {
            BaseAddress = new Uri(baseAddress),
            Timeout = TimeSpan.FromSeconds(30)
        };

        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");

        var persistedOrgId = configuration.GetValue(ApiCurrentOrgIdKey);
        if (Guid.TryParse(persistedOrgId, out var orgId) && orgId != Guid.Empty)
        {
            SetCurrentOrgId(orgId);
        }

        ApplyAuthState();
    }

    public void SetCurrentOrgId(Guid orgId)
    {
        _currentOrgId = orgId;
        _httpClient.DefaultRequestHeaders.Remove("X-Org-Id");
        _httpClient.DefaultRequestHeaders.Add("X-Org-Id", orgId.ToString());
        _configuration.SetLocalValue(ApiCurrentOrgIdKey, orgId.ToString());
    }

    public void ClearCurrentOrgId()
    {
        _currentOrgId = null;
        _httpClient.DefaultRequestHeaders.Remove("X-Org-Id");
        _configuration.SetLocalValue(ApiCurrentOrgIdKey, string.Empty);
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
        private readonly Uri _baseAddress;
        private readonly IAuthSessionService _authSession;
        private readonly SemaphoreSlim _refreshLock = new(1, 1);

        public ApiAuthHandler(Uri baseAddress, IAuthSessionService authSession)
        {
            _baseAddress = baseAddress;
            _authSession = authSession;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var isRefreshEndpoint = request.RequestUri?.AbsolutePath.Contains("/api/auth/refresh", StringComparison.OrdinalIgnoreCase) == true;

            if (!isRefreshEndpoint)
            {
                await EnsureFreshTokenIfNeededAsync(cancellationToken);
                AttachAccessToken(request);
            }

            var response = await base.SendAsync(request, cancellationToken);
            if (response.StatusCode != HttpStatusCode.Unauthorized || isRefreshEndpoint)
                return response;

            response.Dispose();

            var refreshed = await RefreshAccessTokenAsync(cancellationToken);
            if (!refreshed)
                return new HttpResponseMessage(HttpStatusCode.Unauthorized) { RequestMessage = request };

            var retryRequest = await CloneRequestAsync(request, cancellationToken);
            AttachAccessToken(retryRequest);
            return await base.SendAsync(retryRequest, cancellationToken);
        }

        private async Task EnsureFreshTokenIfNeededAsync(CancellationToken cancellationToken)
        {
            if (!_authSession.IsAuthenticated || !_authSession.AccessTokenExpiresAtUtc.HasValue)
                return;

            var remaining = _authSession.AccessTokenExpiresAtUtc.Value - DateTime.UtcNow;
            if (remaining > TimeSpan.FromMinutes(1))
                return;

            await RefreshAccessTokenAsync(cancellationToken);
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
            if (string.IsNullOrWhiteSpace(_authSession.RefreshToken))
                return false;

            await _refreshLock.WaitAsync(cancellationToken);
            try
            {
                if (_authSession.AccessTokenExpiresAtUtc.HasValue &&
                    _authSession.AccessTokenExpiresAtUtc.Value > DateTime.UtcNow.AddMinutes(1))
                {
                    return true;
                }

                using var refreshClient = new HttpClient
                {
                    BaseAddress = _baseAddress,
                    Timeout = TimeSpan.FromSeconds(15)
                };

                var refreshResponse = await refreshClient.PostAsJsonAsync(
                    "api/auth/refresh",
                    new { refreshToken = _authSession.RefreshToken },
                    cancellationToken);

                if (!refreshResponse.IsSuccessStatusCode)
                {
                    _authSession.ClearSession();
                    return false;
                }

                var tokens = await refreshResponse.Content.ReadFromJsonAsync<AuthTokensDto>(cancellationToken: cancellationToken);
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
