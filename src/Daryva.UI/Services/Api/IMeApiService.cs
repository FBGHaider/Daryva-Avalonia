namespace Daryva.Services.Api;

/// <summary>
/// Calls GET /api/me (SaaS) for current user, organisations, and onboarding flags.
/// </summary>
public interface IMeApiService
{
    Task<MeResponseDto?> GetMeAsync(CancellationToken cancellationToken = default);
}
