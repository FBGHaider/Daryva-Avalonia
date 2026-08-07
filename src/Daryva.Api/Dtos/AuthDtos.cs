namespace Daryva.Api.Dtos;

public class RegisterRequest
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class RegisterResponse
{
    public string Email { get; set; } = string.Empty;
    public bool RequiresEmailVerification { get; set; } = true;
    public bool VerificationEmailSent { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class RefreshRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}

public class VerifyEmailRequest
{
    public string Token { get; set; } = string.Empty;
}

public class ResendVerificationEmailRequest
{
    public string Email { get; set; } = string.Empty;
}

public class VerifyEmailResponse
{
    public bool Verified { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class AuthResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime AccessTokenExpiresAt { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

public class LoginResponse
{
    /// <summary>When true, no tokens have been issued yet -- call 2fa/verify with the ChallengeToken and a code.</summary>
    public bool RequiresTwoFactor { get; set; }

    /// <summary>Short-lived token proving password ownership, only valid against 2fa/verify. Set when RequiresTwoFactor is true.</summary>
    public string? ChallengeToken { get; set; }

    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime? AccessTokenExpiresAt { get; set; }
    public string? UserId { get; set; }
    public string? Email { get; set; }
}

public class TwoFactorLoginRequest
{
    public string ChallengeToken { get; set; } = string.Empty;

    /// <summary>Either a 6-digit TOTP code or a one-time recovery code.</summary>
    public string Code { get; set; } = string.Empty;
}

public class ForgotPasswordRequest
{
    public string Email { get; set; } = string.Empty;
}

public class ForgotPasswordResponse
{
    public string Message { get; set; } = string.Empty;
}

public class ResetPasswordRequest
{
    public string Token { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}

public class ResetPasswordResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class AcceptTenantInviteRequest
{
    public string Token { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class AcceptTenantInviteResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime? AccessTokenExpiresAt { get; set; }
    public string? UserId { get; set; }
    public string? Email { get; set; }
}

public class TwoFactorEnrollResponse
{
    /// <summary>Base32 secret, for manual entry if the user can't scan the QR code.</summary>
    public string Secret { get; set; } = string.Empty;

    /// <summary>otpauth:// URI -- render as a QR code client-side.</summary>
    public string OtpAuthUri { get; set; } = string.Empty;
}

public class TwoFactorConfirmRequest
{
    public string Code { get; set; } = string.Empty;
}

public class TwoFactorConfirmResponse
{
    public bool Success { get; set; }

    /// <summary>One-time recovery codes, shown once on successful enrollment. Never retrievable again.</summary>
    public List<string> RecoveryCodes { get; set; } = new();

    public string Message { get; set; } = string.Empty;
}

public class TwoFactorDisableRequest
{
    public string Password { get; set; } = string.Empty;
}

public class TwoFactorDisableResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class TwoFactorRegenerateRecoveryCodesRequest
{
    public string Password { get; set; } = string.Empty;
}

public class TwoFactorRegenerateRecoveryCodesResponse
{
    public bool Success { get; set; }

    /// <summary>New one-time recovery codes, shown once. Replaces all previously issued codes.</summary>
    public List<string> RecoveryCodes { get; set; } = new();

    public string Message { get; set; } = string.Empty;
}

public class MeResponse
{
    public string UserId { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool EmailVerified { get; set; }
    public bool TwoFactorEnabled { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
}
