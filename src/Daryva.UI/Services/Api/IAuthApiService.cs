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

public class MeDto
{
    [JsonPropertyName("userId")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;
}

public interface IAuthApiService
{
    Task<AuthTokensDto> LoginAsync(string email, string password, CancellationToken cancellationToken = default);
    Task<AuthTokensDto> RegisterAsync(string email, string password, CancellationToken cancellationToken = default);
    Task<MeDto?> GetMeAsync(CancellationToken cancellationToken = default);
    Task LogoutAsync(CancellationToken cancellationToken = default);
}
