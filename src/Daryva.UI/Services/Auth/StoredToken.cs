namespace Daryva.Services.Auth;

/// <summary>
/// Token set persisted by <see cref="ITokenStore"/>.
/// </summary>
public sealed class StoredToken
{
    public string AccessToken { get; set; } = string.Empty;
    public string? RefreshToken { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public string? UserId { get; set; }
    public string? Email { get; set; }
}
