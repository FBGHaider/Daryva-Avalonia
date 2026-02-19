using System.Net.Http.Json;

namespace Daryva.Services.Api;

public class AuthApiService : IAuthApiService
{
    private readonly IApiClient _apiClient;
    private readonly IAuthSessionService _authSession;

    public AuthApiService(IApiClient apiClient, IAuthSessionService authSession)
    {
        _apiClient = apiClient;
        _authSession = authSession;
    }

    public async Task<AuthTokensDto> LoginAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        var response = await _apiClient.HttpClient.PostAsJsonAsync("api/auth/login", new { email, password }, cancellationToken);
        response.EnsureSuccessStatusCode();

        var tokens = await response.Content.ReadFromJsonAsync<AuthTokensDto>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Invalid login response.");

        _authSession.SetSession(tokens.AccessToken, tokens.RefreshToken, tokens.AccessTokenExpiresAt, tokens.UserId, tokens.Email);
        _apiClient.ApplyAuthState();
        return tokens;
    }

    public async Task<AuthTokensDto> RegisterAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        var response = await _apiClient.HttpClient.PostAsJsonAsync("api/auth/register", new { email, password }, cancellationToken);
        response.EnsureSuccessStatusCode();

        var tokens = await response.Content.ReadFromJsonAsync<AuthTokensDto>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Invalid register response.");

        _authSession.SetSession(tokens.AccessToken, tokens.RefreshToken, tokens.AccessTokenExpiresAt, tokens.UserId, tokens.Email);
        _apiClient.ApplyAuthState();
        return tokens;
    }

    public async Task<MeDto?> GetMeAsync(CancellationToken cancellationToken = default)
    {
        var response = await _apiClient.HttpClient.GetAsync("api/auth/me", cancellationToken);
        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadFromJsonAsync<MeDto>(cancellationToken: cancellationToken);
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        var refreshToken = _authSession.RefreshToken;
        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            try
            {
                await _apiClient.HttpClient.PostAsJsonAsync("api/auth/logout", new { refreshToken }, cancellationToken);
            }
            catch
            {
            }
        }

        _authSession.ClearSession();
        _apiClient.ApplyAuthState();
        _apiClient.ClearCurrentOrgId();
    }
}
