namespace Daryva.Services.Session;

/// <summary>
/// Current session identity (sub, email). Updated from token on sign-in and from GET /api/me when available.
/// Cleared on sign-out so UI and storage can be scoped to the signed-in user.
/// </summary>
public interface ISessionContext
{
    /// <summary>User id (sub claim or me.user.id). Null when signed out.</summary>
    string? UserId { get; }

    /// <summary>User email. Null when signed out.</summary>
    string? Email { get; }

    /// <summary>True when there is a valid session (token/session present).</summary>
    bool IsAuthenticated { get; }

    /// <summary>Raised when session identity is set (sign-in / api/me) or cleared (sign-out).</summary>
    event EventHandler? SessionChanged;

    /// <summary>Set from token after successful sign-in (before /api/me).</summary>
    void SetFromToken(string userId, string email);

    /// <summary>Update from GET /api/me response (single source of truth for current user).</summary>
    void UpdateFromMe(string? userId, string? email);

    /// <summary>Clear session identity on sign-out. Must be called when user signs out.</summary>
    void Clear();
}
