namespace Daryva.Services.Auth;

/// <summary>Result of a sign-in attempt. When RequiresTwoFactor is true, no session was set -- ChallengeToken must be passed to a 2FA verify step (not yet implemented in this app version).</summary>
public sealed class AuthSignInResult
{
    public bool RequiresTwoFactor { get; init; }
    public string? ChallengeToken { get; init; }
}
