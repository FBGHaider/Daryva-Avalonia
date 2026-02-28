namespace Daryva.Api.Domain;

/// <summary>
/// User profile for OIDC/Dev auth. Id is the provider subject (sub) claim.
/// Created on first login; no password stored when using external identity.
/// </summary>
public class AppUserProfile
{
    /// <summary>
    /// Provider subject (sub) claim - e.g. Auth0/Clerk user id.
    /// </summary>
    public required string Id { get; set; }

    public required string Email { get; set; }
    public string? DisplayName { get; set; }
    public string? Phone { get; set; }
    public string? TimeZoneId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
}
