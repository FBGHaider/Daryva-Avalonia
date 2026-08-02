namespace Daryva.Api.Domain;

/// <summary>
/// Local application user for API-managed email/password authentication.
/// </summary>
public class AppUser
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string? EmailVerificationTokenHash { get; set; }
    public DateTime? EmailVerificationTokenExpiresAt { get; set; }
    public DateTime? EmailVerificationSentAt { get; set; }
    public DateTime? EmailVerifiedAt { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
    public int FailedLoginCount { get; set; }
    public DateTime? LockedUntil { get; set; }
    public string? PasswordResetTokenHash { get; set; }
    public DateTime? PasswordResetTokenExpiresAt { get; set; }
    public DateTime? PasswordResetSentAt { get; set; }

    /// <summary>
    /// Platform-level admin claim -- Daryva staff, not a Landlord's org member. Orthogonal
    /// to OrganizationMember: an admin is not a member of any org's data by default, only
    /// via an explicit, logged Support Session scoped to that org.
    /// </summary>
    public bool IsPlatformAdmin { get; set; }
}

/// <summary>
/// Refresh token session record for local JWT auth.
/// Only token hash is stored for security.
/// </summary>
public class AuthRefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RevokedAt { get; set; }
    public string? CreatedByIp { get; set; }

    public AppUser? User { get; set; }
}
