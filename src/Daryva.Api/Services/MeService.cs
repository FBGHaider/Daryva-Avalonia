using Daryva.Api.Data;
using Daryva.Api.Domain;
using Daryva.Api.Dtos;
using Daryva.Api.Security;
using Microsoft.EntityFrameworkCore;

namespace Daryva.Api.Services;

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

public class MeService : IMeService
{
    private readonly AppDbContext _db;
    private readonly IOrganizationService _orgService;
    private readonly ILogger<MeService> _logger;

    public MeService(AppDbContext db, IOrganizationService orgService, ILogger<MeService> logger)
    {
        _db = db;
        _orgService = orgService;
        _logger = logger;
    }

    public async Task EnsureUserProfileAsync(string sub, string? email, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sub))
            return;

        var normalizedEmail = string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();
        var profile = await _db.AppUserProfiles.FindAsync(new object[] { sub }, cancellationToken);

        if (profile == null)
        {
            profile = new AppUserProfile
            {
                Id = sub,
                Email = normalizedEmail ?? $"{sub}@local",
                DisplayName = null,
                Phone = null,
                TimeZoneId = null,
                CreatedAt = DateTime.UtcNow,
                LastLoginAt = DateTime.UtcNow
            };
            _db.AppUserProfiles.Add(profile);
            _logger.LogInformation("Created AppUserProfile for sub {Sub}, email {Email}", sub, profile.Email);
        }
        else
        {
            profile.LastLoginAt = DateTime.UtcNow;
            if (!string.IsNullOrEmpty(normalizedEmail) && profile.Email != normalizedEmail)
                profile.Email = normalizedEmail;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<MeResponseDto?> GetMeAsync(string userId, CancellationToken cancellationToken = default)
    {
        var profile = await _db.AppUserProfiles.FindAsync(new object[] { userId }, cancellationToken);
        if (profile == null)
            return null;

        var orgList = (await _orgService.GetUserOrganizationsAsync(userId, cancellationToken)).ToList();

        var requiresOrgSetup = orgList.Count == 0;
        var requiresProfileSetup = string.IsNullOrWhiteSpace(profile.DisplayName);

        return new MeResponseDto
        {
            User = new MeUserDto
            {
                Id = profile.Id,
                Email = profile.Email,
                DisplayName = profile.DisplayName,
                Phone = profile.Phone,
                TimeZoneId = profile.TimeZoneId,
                CreatedAt = profile.CreatedAt,
                LastLoginAt = profile.LastLoginAt
            },
            Organisations = orgList,
            RequiresOrgSetup = requiresOrgSetup,
            RequiresProfileSetup = requiresProfileSetup
        };
    }

    public async Task<MeUserDto?> UpdateProfileAsync(string userId, UpdateMeRequest request, CancellationToken cancellationToken = default)
    {
        var profile = await _db.AppUserProfiles.FindAsync(new object[] { userId }, cancellationToken);
        if (profile == null)
            return null;

        if (request.DisplayName != null)
        {
            var name = request.DisplayName.Trim();
            if (name.Length > 256)
                throw new ArgumentException("Display name must be 256 characters or less.", nameof(request.DisplayName));
            profile.DisplayName = string.IsNullOrWhiteSpace(name) ? null : name;
        }

        if (request.Phone != null)
        {
            var phone = request.Phone.Trim();
            if (phone.Length > 50)
                throw new ArgumentException("Phone must be 50 characters or less.", nameof(request.Phone));
            if (!string.IsNullOrEmpty(phone) && !IsValidPhone(phone))
                throw new ArgumentException("Phone contains invalid characters. Use digits, +, spaces, hyphens, parentheses only.", nameof(request.Phone));
            profile.Phone = string.IsNullOrWhiteSpace(phone) ? null : phone;
        }

        if (request.TimeZoneId != null)
        {
            var tz = request.TimeZoneId.Trim();
            if (tz.Length > 128)
                throw new ArgumentException("Time zone id must be 128 characters or less.", nameof(request.TimeZoneId));
            profile.TimeZoneId = string.IsNullOrWhiteSpace(tz) ? null : tz;
        }

        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Updated profile for user {UserId}", userId);

        return new MeUserDto
        {
            Id = profile.Id,
            Email = profile.Email,
            DisplayName = profile.DisplayName,
            Phone = profile.Phone,
            TimeZoneId = profile.TimeZoneId,
            CreatedAt = profile.CreatedAt,
            LastLoginAt = profile.LastLoginAt
        };
    }

    private static bool IsValidPhone(string value)
    {
        return value.All(c => char.IsDigit(c) || c == '+' || char.IsWhiteSpace(c) || c == '-' || c == '(' || c == ')' || c == '.');
    }
}
