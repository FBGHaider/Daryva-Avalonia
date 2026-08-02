using System.Net.Http.Json;
using System.Text.Json;

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

    public async Task<LoginResultDto> LoginAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        var response = await _apiClient.HttpClient.PostAsJsonAsync("api/auth/login", new { email, password }, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        // Backend returns 401 with { error: "Invalid credentials." } (or a lockout message) --
        // surface that instead of EnsureSuccessStatusCode's bare "status code doesn't indicate
        // success", which is meaningless to a user trying to sign in.
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(TryGetErrorMessage(body) ?? "Invalid email or password.");

        var result = JsonSerializer.Deserialize<LoginResultDto>(body)
            ?? throw new InvalidOperationException("Invalid login response.");

        if (!result.RequiresTwoFactor && !string.IsNullOrWhiteSpace(result.AccessToken))
        {
            _authSession.SetSession(
                result.AccessToken,
                result.RefreshToken ?? string.Empty,
                result.AccessTokenExpiresAt ?? DateTime.UtcNow,
                result.UserId ?? string.Empty,
                result.Email ?? string.Empty);
            _apiClient.ApplyAuthState();
        }

        return result;
    }

    public async Task<AuthTokensDto> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var response = await _apiClient.HttpClient.PostAsJsonAsync("api/auth/refresh", new { refreshToken }, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<AuthTokensDto>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Invalid refresh response.");
    }

    public async Task<RegisterResultDto> RegisterAsync(string email, string password, string? firstName = null, string? lastName = null, CancellationToken cancellationToken = default)
    {
        var response = await _apiClient.HttpClient.PostAsJsonAsync("api/auth/register", new { email, password, firstName, lastName }, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        // Same fix as LoginAsync: e.g. 409 for an already-registered email should show the
        // backend's actual message, not a bare status code.
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(TryGetErrorMessage(body) ?? "Failed to create account.");

        return JsonSerializer.Deserialize<RegisterResultDto>(body)
            ?? throw new InvalidOperationException("Invalid register response.");
    }

    public async Task<VerifyEmailResultDto> VerifyEmailAsync(string token, CancellationToken cancellationToken = default)
    {
        var response = await _apiClient.HttpClient.PostAsJsonAsync("api/auth/verify-email", new { token }, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<VerifyEmailResultDto>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Invalid verify email response.");
    }

    public async Task<RegisterResultDto> ResendVerificationEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var response = await _apiClient.HttpClient.PostAsJsonAsync("api/auth/resend-verification", new { email }, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<RegisterResultDto>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Invalid resend verification response.");
    }

    public async Task<ForgotPasswordResultDto> ForgotPasswordAsync(string email, CancellationToken cancellationToken = default)
    {
        var response = await _apiClient.HttpClient.PostAsJsonAsync("api/auth/forgot-password", new { email }, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<ForgotPasswordResultDto>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Invalid forgot-password response.");
    }

    public async Task<ResetPasswordResultDto> ResetPasswordAsync(string token, string newPassword, CancellationToken cancellationToken = default)
    {
        var response = await _apiClient.HttpClient.PostAsJsonAsync("api/auth/reset-password", new { token, newPassword }, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        // Backend returns 400 with a meaningful { success:false, message:"..." } body for an
        // invalid/expired token, and a differently-shaped { error:"..." } body for a rejected new
        // password (ArgumentException) -- surface whichever message is actually present instead of
        // just throwing on the non-2xx status (which would give a bare "status code" message).
        try
        {
            var result = JsonSerializer.Deserialize<ResetPasswordResultDto>(body);
            if (result != null && !string.IsNullOrWhiteSpace(result.Message))
                return result;
        }
        catch (JsonException)
        {
        }

        if (response.IsSuccessStatusCode)
            return new ResetPasswordResultDto { Success = true, Message = "Password has been reset." };

        return new ResetPasswordResultDto { Success = false, Message = TryGetErrorMessage(body) ?? "Failed to reset password." };
    }

    /// <summary>Parses the API's generic `{ error: "..." }` failure body; null if the body isn't shaped that way.</summary>
    private static string? TryGetErrorMessage(string body)
    {
        try
        {
            var error = JsonSerializer.Deserialize<ErrorResponseDto>(body);
            return string.IsNullOrWhiteSpace(error?.Error) ? null : error.Error;
        }
        catch (JsonException)
        {
            return null;
        }
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
