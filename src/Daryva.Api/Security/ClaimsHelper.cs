using System.Security.Claims;

namespace Daryva.Api.Security;

/// <summary>
/// Helpers for reading claims from the current user (e.g. Clerk/OIDC JWT).
/// </summary>
public static class ClaimsHelper
{
    /// <summary>
    /// Gets the user's email from common JWT claim names (Clerk uses "email"; some providers use ClaimTypes.Email or "preferred_username").
    /// </summary>
    public static string? GetEmailFromClaims(ClaimsPrincipal user)
    {
        if (user == null) return null;

        var email = user.FindFirstValue(ClaimTypes.Email)
            ?? user.FindFirstValue("email")
            ?? user.FindFirstValue("preferred_username");

        return string.IsNullOrWhiteSpace(email) ? null : email.Trim();
    }
}
