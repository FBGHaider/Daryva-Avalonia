using Daryva.Services.Api;

namespace Daryva.Services.Session;

/// <summary>
/// Session identity backed by auth session; updated from token and /api/me. Cleared on sign-out.
/// </summary>
public sealed class SessionContext : ISessionContext
{
    private readonly IAuthSessionService _authSession;
    private string? _userId;
    private string? _email;

    public SessionContext(IAuthSessionService authSession)
    {
        _authSession = authSession ?? throw new ArgumentNullException(nameof(authSession));
    }

    public string? UserId => _userId ?? _authSession.UserId;
    public string? Email => _email ?? _authSession.Email;
    public bool IsAuthenticated => _authSession.IsAuthenticated;

    public event EventHandler? SessionChanged;

    public void SetFromToken(string userId, string email)
    {
        _userId = userId ?? string.Empty;
        _email = email ?? string.Empty;
        System.Diagnostics.Debug.WriteLine($"[Session] SetFromToken: UserId present={!string.IsNullOrEmpty(_userId)}, Email present={!string.IsNullOrEmpty(_email)}");
        SessionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void UpdateFromMe(string? userId, string? email)
    {
        _userId = userId;
        _email = email ?? string.Empty;
        System.Diagnostics.Debug.WriteLine($"[Session] UpdateFromMe: UserId present={!string.IsNullOrEmpty(_userId)}, Email present={!string.IsNullOrEmpty(_email)}");
        SessionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Clear()
    {
        _userId = null;
        _email = null;
        System.Diagnostics.Debug.WriteLine("[Session] Clear");
        SessionChanged?.Invoke(this, EventArgs.Empty);
    }
}
