using System.Text.Json.Serialization;

namespace Daryva.Services.Api;

public class AuthTokensDto
{
    [JsonPropertyName("accessToken")]
    public string AccessToken { get; set; } = string.Empty;

    [JsonPropertyName("refreshToken")]
    public string RefreshToken { get; set; } = string.Empty;

    [JsonPropertyName("accessTokenExpiresAt")]
    public DateTime AccessTokenExpiresAt { get; set; }

    [JsonPropertyName("userId")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;
}

public class LoginResultDto
{
    [JsonPropertyName("requiresTwoFactor")]
    public bool RequiresTwoFactor { get; set; }

    [JsonPropertyName("challengeToken")]
    public string? ChallengeToken { get; set; }

    [JsonPropertyName("accessToken")]
    public string? AccessToken { get; set; }

    [JsonPropertyName("refreshToken")]
    public string? RefreshToken { get; set; }

    [JsonPropertyName("accessTokenExpiresAt")]
    public DateTime? AccessTokenExpiresAt { get; set; }

    [JsonPropertyName("userId")]
    public string? UserId { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }
}

public class MeDto
{
    [JsonPropertyName("userId")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("firstName")]
    public string FirstName { get; set; } = string.Empty;

    [JsonPropertyName("lastName")]
    public string LastName { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;
}

public class RegisterResultDto
{
    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("requiresEmailVerification")]
    public bool RequiresEmailVerification { get; set; }

    [JsonPropertyName("verificationEmailSent")]
    public bool VerificationEmailSent { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}

public class VerifyEmailResultDto
{
    [JsonPropertyName("verified")]
    public bool Verified { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}

public class ForgotPasswordResultDto
{
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}

public class ResetPasswordResultDto
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}

/// <summary>Shape of the API's generic `{ error: "..." }` failure body (ArgumentException catch blocks).</summary>
public class ErrorResponseDto
{
    [JsonPropertyName("error")]
    public string? Error { get; set; }
}

public interface IAuthApiService
{
    Task<LoginResultDto> LoginAsync(string email, string password, CancellationToken cancellationToken = default);
    Task<AuthTokensDto> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task<RegisterResultDto> RegisterAsync(string email, string password, string? firstName = null, string? lastName = null, CancellationToken cancellationToken = default);
    Task<VerifyEmailResultDto> VerifyEmailAsync(string token, CancellationToken cancellationToken = default);
    Task<RegisterResultDto> ResendVerificationEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<ForgotPasswordResultDto> ForgotPasswordAsync(string email, CancellationToken cancellationToken = default);
    Task<ResetPasswordResultDto> ResetPasswordAsync(string token, string newPassword, CancellationToken cancellationToken = default);
    Task<MeDto?> GetMeAsync(CancellationToken cancellationToken = default);
    Task LogoutAsync(CancellationToken cancellationToken = default);
}
