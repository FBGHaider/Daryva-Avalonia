namespace Daryva.Services.Api;

public interface IAuthSessionService
{
    bool IsAuthenticated { get; }
    string? AccessToken { get; }
    string? RefreshToken { get; }
    DateTime? AccessTokenExpiresAtUtc { get; }
    string? UserId { get; }
    string? Email { get; }

    void SetSession(string accessToken, string refreshToken, DateTime accessTokenExpiresAtUtc, string userId, string email);
    void UpdateAccessToken(string accessToken, DateTime accessTokenExpiresAtUtc);
    void ClearSession();
}
