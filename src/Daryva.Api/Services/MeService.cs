using Daryva.Api.Data;
using Daryva.Api.Domain;
using Daryva.Api.Dtos;
using Daryva.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Daryva.Api.Services;

public class MeService : IMeService
{
    private readonly AppDbContext _db;
    private readonly IOrganizationService _orgService;
    private readonly ILogger<MeService> _logger;
    private readonly string _devUserId;
    private readonly bool _devAuthEnabled;

    public MeService(AppDbContext db, IOrganizationService orgService, ILogger<MeService> logger, IConfiguration configuration)
    {
        _db = db;
        _orgService = orgService;
        _logger = logger;
        _devUserId = configuration.GetValue<string>("DevAuth:UserId") ?? "dev-user-1";
        _devAuthEnabled = configuration.GetValue<bool>("DevAuth:Enabled");
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

        // Backfill OrganisationMember.Email for this user so they show in the members list
        if (!string.IsNullOrEmpty(normalizedEmail))
        {
            var membersWithNullEmail = await _db.OrganizationMembers
                .Where(m => m.UserId == sub && (m.Email == null || m.Email == ""))
                .ToListAsync(cancellationToken);
            foreach (var m in membersWithNullEmail)
            {
                m.Email = normalizedEmail;
            }

            // Migrate dev placeholder to real identity: org members created as dev-user-1 / dev@local
            // become this user so the team list shows the signed-in email. Local-dev-only convenience
            // for reconciling DevAuthMiddleware-created test data with a real login -- gated on
            // DevAuth:Enabled (never true in production) because it is otherwise completely unscoped:
            // it hands over EVERY dev-user-1/@local org membership to whichever real account happens
            // to call /api/me next, real or not. This previously ran unconditionally and, the first
            // time this query stopped throwing (see the EndsWith fix above), silently reassigned two
            // real production organizations to an unrelated verification test account.
            var devPlaceholderMembers = new List<OrganizationMember>();
            if (_devAuthEnabled && !string.Equals(sub, _devUserId, StringComparison.OrdinalIgnoreCase))
            {
                // EndsWith(string, StringComparison) can't be translated to SQL by Npgsql -- emails are
                // already normalized lowercase everywhere they're written (see AuthService.NormalizeEmail),
                // so a plain lowercase comparison is equivalent and translates to a normal SQL LIKE.
                devPlaceholderMembers = await _db.OrganizationMembers
                    .Where(m => m.UserId == _devUserId && m.Email != null && m.Email.ToLower().EndsWith("@local"))
                    .ToListAsync(cancellationToken);
                foreach (var m in devPlaceholderMembers)
                {
                    m.UserId = sub;
                    m.Email = normalizedEmail;
                    _logger.LogInformation("Migrated org member from dev placeholder to user {Sub} in org {OrgId}", sub, m.OrganizationId);
                }
            }

            if (membersWithNullEmail.Count > 0 || devPlaceholderMembers.Count > 0)
                await _db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<MeResponseDto?> GetMeAsync(string userId, CancellationToken cancellationToken = default)
    {
        var profile = await _db.AppUserProfiles.FindAsync(new object[] { userId }, cancellationToken);
        if (profile == null)
            return null;

        var orgList = (await _orgService.GetUserOrganizationsAsync(userId, cancellationToken)).ToList();

        // If user has no Daryva API orgs (e.g. first sign-in or only created org in Clerk), auto-create one
        // so the app can go straight to dashboard instead of showing setup.
        if (orgList.Count == 0)
        {
            var suggestedName = !string.IsNullOrWhiteSpace(profile.DisplayName)
                ? $"{profile.DisplayName}'s organization"
                : null;
            await _orgService.EnsureDefaultOrganizationAsync(userId, suggestedName, cancellationToken);
            orgList = (await _orgService.GetUserOrganizationsAsync(userId, cancellationToken)).ToList();
        }

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
