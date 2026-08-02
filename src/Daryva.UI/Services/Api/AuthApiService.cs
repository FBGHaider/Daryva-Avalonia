using System.Net;
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
            throw new InvalidOperationException(TryGetErrorMessage(response, body) ?? "Invalid email or password.");

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

    public async Task<AuthTokensDto> VerifyTwoFactorAsync(string challengeToken, string code, CancellationToken cancellationToken = default)
    {
        var response = await _apiClient.HttpClient.PostAsJsonAsync("api/auth/2fa/verify", new { challengeToken, code }, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(TryGetErrorMessage(response, body) ?? "Invalid or expired code.");

        var tokens = JsonSerializer.Deserialize<AuthTokensDto>(body)
            ?? throw new InvalidOperationException("Invalid two-factor verification response.");

        _authSession.SetSession(tokens.AccessToken, tokens.RefreshToken, tokens.AccessTokenExpiresAt, tokens.UserId, tokens.Email);
        _apiClient.ApplyAuthState();
        return tokens;
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
            throw new InvalidOperationException(TryGetErrorMessage(response, body) ?? "Failed to create account.");

        return JsonSerializer.Deserialize<RegisterResultDto>(body)
            ?? throw new InvalidOperationException("Invalid register response.");
    }

    public async Task<VerifyEmailResultDto> VerifyEmailAsync(string token, CancellationToken cancellationToken = default)
    {
        var response = await _apiClient.HttpClient.PostAsJsonAsync("api/auth/verify-email", new { token }, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        // Backend always sets a friendly Message on both success and failure (invalid/expired
        // token) -- only a genuinely malformed body (e.g. a 429 with no "verified" field) should
        // fall through to TryGetErrorMessage.
        try
        {
            var result = JsonSerializer.Deserialize<VerifyEmailResultDto>(body);
            if (result != null && !string.IsNullOrWhiteSpace(result.Message))
                return result;
        }
        catch (JsonException)
        {
        }

        return new VerifyEmailResultDto { Verified = false, Message = TryGetErrorMessage(response, body) ?? "Failed to verify email." };
    }

    public async Task<RegisterResultDto> ResendVerificationEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var response = await _apiClient.HttpClient.PostAsJsonAsync("api/auth/resend-verification", new { email }, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        try
        {
            var result = JsonSerializer.Deserialize<RegisterResultDto>(body);
            if (result != null && !string.IsNullOrWhiteSpace(result.Message))
                return result;
        }
        catch (JsonException)
        {
        }

        return new RegisterResultDto { Email = email, Message = TryGetErrorMessage(response, body) ?? "Failed to resend verification email." };
    }

    public async Task<ForgotPasswordResultDto> ForgotPasswordAsync(string email, CancellationToken cancellationToken = default)
    {
        var response = await _apiClient.HttpClient.PostAsJsonAsync("api/auth/forgot-password", new { email }, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        try
        {
            var result = JsonSerializer.Deserialize<ForgotPasswordResultDto>(body);
            if (result != null && !string.IsNullOrWhiteSpace(result.Message))
                return result;
        }
        catch (JsonException)
        {
        }

        return new ForgotPasswordResultDto { Message = TryGetErrorMessage(response, body) ?? "Failed to process request." };
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

        return new ResetPasswordResultDto { Success = false, Message = TryGetErrorMessage(response, body) ?? "Failed to reset password." };
    }

    public async Task<TwoFactorEnrollResultDto> EnrollTwoFactorAsync(CancellationToken cancellationToken = default)
    {
        var response = await _apiClient.HttpClient.PostAsync("api/auth/2fa/enroll", null, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(TryGetErrorMessage(response, body) ?? "Failed to start two-factor enrollment.");

        return JsonSerializer.Deserialize<TwoFactorEnrollResultDto>(body)
            ?? throw new InvalidOperationException("Invalid enrollment response.");
    }

    public async Task<TwoFactorConfirmResultDto> ConfirmTwoFactorAsync(string code, CancellationToken cancellationToken = default)
    {
        var response = await _apiClient.HttpClient.PostAsJsonAsync("api/auth/2fa/confirm", new { code }, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        // Both success (200) and a rejected code (400) return the same { success, recoveryCodes,
        // message } shape here, unlike ResetPasswordAsync -- but still fall back on a genuinely
        // malformed/500 body instead of surfacing an empty Message.
        try
        {
            var result = JsonSerializer.Deserialize<TwoFactorConfirmResultDto>(body);
            if (result != null && !string.IsNullOrWhiteSpace(result.Message))
                return result;
        }
        catch (JsonException)
        {
        }

        return new TwoFactorConfirmResultDto { Success = false, Message = TryGetErrorMessage(response, body) ?? "Invalid verification code." };
    }

    public async Task<TwoFactorDisableResultDto> DisableTwoFactorAsync(string password, CancellationToken cancellationToken = default)
    {
        var response = await _apiClient.HttpClient.PostAsJsonAsync("api/auth/2fa/disable", new { password }, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        try
        {
            var result = JsonSerializer.Deserialize<TwoFactorDisableResultDto>(body);
            if (result != null && !string.IsNullOrWhiteSpace(result.Message))
                return result;
        }
        catch (JsonException)
        {
        }

        return new TwoFactorDisableResultDto { Success = false, Message = TryGetErrorMessage(response, body) ?? "Failed to disable two-factor authentication." };
    }

    public async Task<TwoFactorRegenerateRecoveryCodesResultDto> RegenerateRecoveryCodesAsync(string password, CancellationToken cancellationToken = default)
    {
        var response = await _apiClient.HttpClient.PostAsJsonAsync("api/auth/2fa/recovery-codes/regenerate", new { password }, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        try
        {
            var result = JsonSerializer.Deserialize<TwoFactorRegenerateRecoveryCodesResultDto>(body);
            if (result != null && !string.IsNullOrWhiteSpace(result.Message))
                return result;
        }
        catch (JsonException)
        {
        }

        return new TwoFactorRegenerateRecoveryCodesResultDto { Success = false, Message = TryGetErrorMessage(response, body) ?? "Failed to regenerate recovery codes." };
    }

    /// <summary>
    /// Parses the API's generic `{ error, message }` failure body (preferring the friendlier
    /// "message" field when present -- only the rate limiter's 429 body sets both; ArgumentException/
    /// UnauthorizedAccessException bodies only ever set "error", which is already user-facing text).
    /// For 429s specifically, appends how long to wait using the Retry-After header, since the
    /// generic "please wait before trying again" text alone doesn't say how long.
    /// </summary>
    private static string? TryGetErrorMessage(HttpResponseMessage response, string body)
    {
        string? parsed;
        try
        {
            var error = JsonSerializer.Deserialize<ErrorResponseDto>(body);
            parsed = !string.IsNullOrWhiteSpace(error?.Message) ? error.Message : error?.Error;
        }
        catch (JsonException)
        {
            parsed = null;
        }

        if (response.StatusCode != HttpStatusCode.TooManyRequests)
            return parsed;

        var message = parsed ?? "Too many attempts. Please wait before trying again.";
        var retryAfter = response.Headers.RetryAfter?.Delta;
        if (retryAfter is { } wait && wait > TimeSpan.Zero)
            message = $"{message.TrimEnd('.')}. Try again in {FormatWait(wait)}.";

        return message;
    }

    private static string FormatWait(TimeSpan wait)
    {
        var totalSeconds = (int)Math.Ceiling(wait.TotalSeconds);
        if (totalSeconds <= 60)
            return $"{totalSeconds} second{(totalSeconds == 1 ? "" : "s")}";

        var minutes = (int)Math.Ceiling(wait.TotalMinutes);
        return $"{minutes} minute{(minutes == 1 ? "" : "s")}";
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
