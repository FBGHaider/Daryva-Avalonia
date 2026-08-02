using Daryva.Api.Dtos;

namespace Daryva.Api.Services.Interfaces;

public interface IAuthService
{
    Task<RegisterResponse> RegisterAsync(RegisterRequest request, string? clientIp, CancellationToken cancellationToken = default);
    Task<VerifyEmailResponse> VerifyEmailAsync(string token, CancellationToken cancellationToken = default);
    Task<RegisterResponse> ResendVerificationEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<AuthResponse?> LoginAsync(LoginRequest request, string? clientIp, CancellationToken cancellationToken = default);
    Task<AuthResponse?> RefreshAsync(string refreshToken, string? clientIp, CancellationToken cancellationToken = default);
    Task<bool> LogoutAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task<MeResponse?> GetMeAsync(string userId, CancellationToken cancellationToken = default);
    Task<ForgotPasswordResponse> ForgotPasswordAsync(string email, string? clientIp, CancellationToken cancellationToken = default);
    Task<ResetPasswordResponse> ResetPasswordAsync(string token, string newPassword, string? clientIp, CancellationToken cancellationToken = default);
}
