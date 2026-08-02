using Daryva.Api.Dtos;

namespace Daryva.Api.Services.Interfaces;

/// <summary>
/// Service for GET /api/me: user profile, organisations, onboarding state.
/// Ensures AppUserProfile exists on first login (OIDC/Dev).
/// </summary>
public interface IMeService
{
    /// <summary>
    /// Ensure a profile exists for the given subject and email (from JWT/Dev). Create if not exists.
    /// </summary>
    Task EnsureUserProfileAsync(string sub, string? email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get full /api/me response: user, organisations, requiresOrgSetup, requiresProfileSetup.
    /// </summary>
    Task<MeResponseDto?> GetMeAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update current user profile (DisplayName, Phone, TimeZoneId). Validates and persists.
    /// </summary>
    Task<MeUserDto?> UpdateProfileAsync(string userId, UpdateMeRequest request, CancellationToken cancellationToken = default);
}
