using System.Text.Json;
using Daryva.Services.Platform;

namespace Daryva.Services.Api;

public class AuthSessionService : IAuthSessionService
{
    private const string SecureStoreKey = "ApiAuthSession";
    private readonly ISecureStore _secureStore;

    public bool IsAuthenticated =>
        !string.IsNullOrWhiteSpace(AccessToken) &&
        !string.IsNullOrWhiteSpace(RefreshToken) &&
        AccessTokenExpiresAtUtc.HasValue;

    public string? AccessToken { get; private set; }
    public string? RefreshToken { get; private set; }
    public DateTime? AccessTokenExpiresAtUtc { get; private set; }
    public string? UserId { get; private set; }
    public string? Email { get; private set; }

    public AuthSessionService(ISecureStore secureStore)
    {
        _secureStore = secureStore;
        Load();
    }

    public void SetSession(string accessToken, string refreshToken, DateTime accessTokenExpiresAtUtc, string userId, string email)
    {
        AccessToken = accessToken;
        RefreshToken = refreshToken;
        AccessTokenExpiresAtUtc = DateTime.SpecifyKind(accessTokenExpiresAtUtc, DateTimeKind.Utc);
        UserId = userId;
        Email = email;
        Save();
    }

    public void UpdateAccessToken(string accessToken, DateTime accessTokenExpiresAtUtc)
    {
        AccessToken = accessToken;
        AccessTokenExpiresAtUtc = DateTime.SpecifyKind(accessTokenExpiresAtUtc, DateTimeKind.Utc);
        Save();
    }

    public void ClearSession()
    {
        AccessToken = null;
        RefreshToken = null;
        AccessTokenExpiresAtUtc = null;
        UserId = null;
        Email = null;
        _secureStore.Remove(SecureStoreKey);
    }

    private void Load()
    {
        try
        {
            var raw = _secureStore.Retrieve(SecureStoreKey);
            if (string.IsNullOrWhiteSpace(raw))
                return;

            var session = JsonSerializer.Deserialize<AuthSessionData>(raw);
            if (session == null)
                return;

            AccessToken = session.AccessToken;
            RefreshToken = session.RefreshToken;
            AccessTokenExpiresAtUtc = session.AccessTokenExpiresAtUtc;
            UserId = session.UserId;
            Email = session.Email;
        }
        catch
        {
            ClearSession();
        }
    }

    private void Save()
    {
        var payload = new AuthSessionData
        {
            AccessToken = AccessToken,
            RefreshToken = RefreshToken,
            AccessTokenExpiresAtUtc = AccessTokenExpiresAtUtc,
            UserId = UserId,
            Email = Email
        };

        _secureStore.Store(SecureStoreKey, JsonSerializer.Serialize(payload));
    }

    private sealed class AuthSessionData
    {
        public string? AccessToken { get; set; }
        public string? RefreshToken { get; set; }
        public DateTime? AccessTokenExpiresAtUtc { get; set; }
        public string? UserId { get; set; }
        public string? Email { get; set; }
    }
}
