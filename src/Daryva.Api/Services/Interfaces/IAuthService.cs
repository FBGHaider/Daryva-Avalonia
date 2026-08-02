using Daryva.Api.Dtos;

namespace Daryva.Api.Services.Interfaces;

public interface IAuthService
{
    Task<RegisterResponse> RegisterAsync(RegisterRequest request, string? clientIp, CancellationToken cancellationToken = default);
    Task<VerifyEmailResponse> VerifyEmailAsync(string token, CancellationToken cancellationToken = default);
    Task<RegisterResponse> ResendVerificationEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<LoginResponse?> LoginAsync(LoginRequest request, string? clientIp, CancellationToken cancellationToken = default);

    /// <summary>Completes a 2FA-challenged login: verifies a TOTP code or recovery code against the challenge token's user, then issues tokens.</summary>
    Task<AuthResponse?> VerifyTwoFactorLoginAsync(string challengeToken, string code, string? clientIp, CancellationToken cancellationToken = default);
    Task<AuthResponse?> RefreshAsync(string refreshToken, string? clientIp, CancellationToken cancellationToken = default);
    Task<bool> LogoutAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task<MeResponse?> GetMeAsync(string userId, CancellationToken cancellationToken = default);
    Task<ForgotPasswordResponse> ForgotPasswordAsync(string email, string? clientIp, CancellationToken cancellationToken = default);
    Task<ResetPasswordResponse> ResetPasswordAsync(string token, string newPassword, string? clientIp, CancellationToken cancellationToken = default);

    /// <summary>Starts (or restarts) TOTP enrollment: generates a new secret, stores it encrypted with TwoFactorEnabled still false.</summary>
    Task<TwoFactorEnrollResponse> EnrollTwoFactorAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>Verifies the code against the pending secret; on success sets TwoFactorEnabled and returns one-time recovery codes.</summary>
    Task<TwoFactorConfirmResponse> ConfirmTwoFactorAsync(string userId, string code, CancellationToken cancellationToken = default);
}
