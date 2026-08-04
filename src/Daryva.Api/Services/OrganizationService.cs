using Daryva.Api.Domain;
using Daryva.Api.Dtos;
using Daryva.Api.Repositories.Interfaces;
using Daryva.Api.Security.Interfaces;
using Daryva.Api.Services.Interfaces;
using System.Security.Cryptography;
using System.Text;

namespace Daryva.Api.Services;

/// <summary>
/// Implementation of IOrganizationService.
/// </summary>
public class OrganizationService : IOrganizationService
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IOrganizationMemberRepository _memberRepository;
    private readonly IAppUserRepository _appUserRepository;
    private readonly IAppUserProfileRepository _appUserProfileRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<OrganizationService> _logger;

    public OrganizationService(
        IOrganizationRepository organizationRepository,
        IOrganizationMemberRepository memberRepository,
        IAppUserRepository appUserRepository,
        IAppUserProfileRepository appUserProfileRepository,
        IUnitOfWork unitOfWork,
        ITenantContext tenantContext,
        ILogger<OrganizationService> logger)
    {
        _organizationRepository = organizationRepository;
        _memberRepository = memberRepository;
        _appUserRepository = appUserRepository;
        _appUserProfileRepository = appUserProfileRepository;
        _unitOfWork = unitOfWork;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<OrganizationResponse> CreateOrganizationAsync(
        string userId,
        CreateOrganizationRequest request,
        string? callerEmail = null,
        CancellationToken cancellationToken = default)
    {
        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Organization name cannot be empty.", nameof(request.Name));
        if (name.Length > 256)
            throw new ArgumentException("Organization name must be 256 characters or less.", nameof(request.Name));

        var org = new Organization
        {
            Id = Guid.NewGuid(),
            Name = name,
            CreatedAt = DateTime.UtcNow
        };

        // Use email from JWT/claims when provided so the owner shows the signed-in email; otherwise resolve from profile
        var ownerEmail = !string.IsNullOrWhiteSpace(callerEmail)
            ? callerEmail.Trim().ToLowerInvariant()
            : await ResolveUserEmailAsync(userId, cancellationToken);

        // Creator becomes the org's Landlord and primary owner
        var member = new OrganizationMember
        {
            Id = Guid.NewGuid(),
            OrganizationId = org.Id,
            UserId = userId,
            Email = ownerEmail,
            Role = OrganizationMember.Roles.Landlord,
            IsPrimaryOwner = true,
            JoinedAt = DateTime.UtcNow
        };

        _organizationRepository.Add(org);
        _memberRepository.Add(member);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created organization {OrgId} with owner {UserId}.", org.Id, userId);

        return MapToResponse(org, OrganizationMember.Roles.Landlord);
    }

    public async Task<IEnumerable<OrganizationResponse>> GetUserOrganizationsAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var memberships = await _memberRepository.GetByUserIdWithOrganizationAsync(userId, cancellationToken);

        return memberships
            .Where(m => m.Organization != null)
            .Select(m => MapToResponse(m.Organization!, m.Role))
            .ToList();
    }

    public async Task EnsureDefaultOrganizationAsync(
        string userId,
        string? suggestedName,
        CancellationToken cancellationToken = default)
    {
        var existing = await _memberRepository.AnyForUserAsync(userId, cancellationToken);
        if (existing)
            return;

        var name = string.IsNullOrWhiteSpace(suggestedName)
            ? "My organization"
            : suggestedName.Trim().Length > 256
                ? suggestedName.Trim().Substring(0, 256)
                : suggestedName.Trim();

        await CreateOrganizationAsync(userId, new CreateOrganizationRequest { Name = name }, callerEmail: null, cancellationToken);
        _logger.LogInformation("Auto-created default organization for user {UserId} (no Daryva orgs existed).", userId);
    }

    public async Task<OrganizationResponse?> GetOrganizationAsync(
        Guid orgId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var membership = await GetCallerMembershipAsync(orgId, userId, cancellationToken);
        if (membership == null)
            return null;

        var org = await _organizationRepository.GetByIdAsync(orgId, cancellationToken);

        return org == null ? null : MapToResponse(org, membership.Role);
    }

    public async Task<OrganizationResponse?> UpdateOrganizationAsync(
        Guid orgId,
        string userId,
        UpdateOrganizationRequest request,
        CancellationToken cancellationToken = default)
    {
        var membership = await GetCallerMembershipAsync(orgId, userId, cancellationToken);
        if (membership == null)
            return null;
        if (!membership.IsPrimaryOwner)
            throw new InvalidOperationException("Only the owner can rename the organization.");

        var org = await _organizationRepository.GetByIdAsync(orgId, cancellationToken);
        if (org == null)
            return null;

        if (request.Name != null)
        {
            var name = request.Name.Trim();
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Organization name cannot be empty.", nameof(request.Name));
            if (name.Length > 256)
                throw new ArgumentException("Organization name must be 256 characters or less.", nameof(request.Name));
            org.Name = name;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("User {UserId} updated organization {OrgId} (name).", userId, orgId);
        return MapToResponse(org, membership.Role);
    }

    public async Task<OrganizationMemberResponse> AddMemberAsync(
        Guid orgId,
        string userId,
        AddMemberRequest request,
        CancellationToken cancellationToken = default)
    {
        var callerMembership = await GetCallerMembershipAsync(orgId, userId, cancellationToken);
        if (callerMembership == null)
            throw new InvalidOperationException("You are not a member of this organization.");

        // Validate role
        if (!OrganizationMember.Roles.IsValid(request.Role))
            throw new ArgumentException($"Invalid role: {request.Role}.", nameof(request.Role));

        // Check if member already exists by email
        var existingMember = await _memberRepository.GetByEmailAsync(orgId, request.Email, cancellationToken);
        if (existingMember != null)
            throw new InvalidOperationException($"User with email {request.Email} is already a member.");

        var newMember = new OrganizationMember
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            UserId = Guid.NewGuid().ToString(), // Placeholder; update when user logs in
            Email = request.Email.Trim().ToLower(),
            Role = request.Role,
            JoinedAt = DateTime.UtcNow
        };

        _memberRepository.Add(newMember);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Added member {Email} to organization {OrgId} with role {Role}.",
            request.Email, orgId, request.Role);

        return MapToResponse(newMember);
    }

    public async Task<bool> DeleteOrganizationAsync(
        Guid orgId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var membership = await GetCallerMembershipAsync(orgId, userId, cancellationToken);
        if (membership == null)
            return false;

        if (!membership.IsPrimaryOwner)
            throw new InvalidOperationException("Only organization owners can delete an organization.");

        var org = await _organizationRepository.GetByIdAsync(orgId, cancellationToken);
        if (org == null)
            return false;

        _organizationRepository.Remove(org);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Organization {OrgId} deleted by user {UserId}.", orgId, userId);
        return true;
    }

    public async Task<IEnumerable<OrganizationMemberResponse>> GetOrganizationMembersAsync(
        Guid orgId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var membership = await GetCallerMembershipAsync(orgId, userId, cancellationToken);
        if (membership == null)
            return Enumerable.Empty<OrganizationMemberResponse>();

        var members = await _memberRepository.GetByOrganizationIdAsync(orgId, cancellationToken);

        var userIds = members.Select(m => m.UserId).Distinct();
        var profiles = await _appUserProfileRepository.GetByIdsAsync(userIds, cancellationToken);

        return members.Select(m => MapToResponseWithProfile(m, profiles.GetValueOrDefault(m.UserId))).ToList();
    }

    public async Task<CreateOrgInviteResponse> CreateInviteAsync(
        Guid orgId,
        string userId,
        CreateOrgInviteRequest request,
        CancellationToken cancellationToken = default)
    {
        var callerMembership = await GetCallerMembershipAsync(orgId, userId, cancellationToken);
        if (callerMembership == null)
            throw new InvalidOperationException("You are not a member of this organization.");

        if (!CanManageJoin(callerMembership))
            throw new InvalidOperationException("Only the organization owner can create invites.");

        if (!OrganizationMember.Roles.IsValid(request.Role))
            throw new ArgumentException($"Invalid role: {request.Role}.", nameof(request.Role));

        if (request.ExpiresInDays <= 0 || request.ExpiresInDays > 30)
            throw new ArgumentException("ExpiresInDays must be between 1 and 30.", nameof(request.ExpiresInDays));

        var inviteToken = GenerateSecureToken();
        var invite = new OrganizationInvite
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            TokenHash = HashSecret(inviteToken),
            Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim().ToLowerInvariant(),
            Role = request.Role,
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(request.ExpiresInDays)
        };

        _organizationRepository.AddInvite(invite);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreateOrgInviteResponse
        {
            InviteId = invite.Id,
            OrganizationId = orgId,
            Token = inviteToken,
            Email = invite.Email,
            Role = invite.Role,
            ExpiresAt = invite.ExpiresAt
        };
    }

    public async Task<JoinOrganizationResponse> AcceptInviteAsync(
        string userId,
        AcceptOrgInviteRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
            throw new ArgumentException("Invite token is required.", nameof(request.Token));

        var tokenHash = HashSecret(request.Token.Trim());
        var invite = await _organizationRepository.GetInviteByTokenHashAsync(tokenHash, cancellationToken);

        if (invite == null || invite.RevokedAt != null || invite.UsedAt != null || invite.ExpiresAt <= DateTime.UtcNow)
            throw new InvalidOperationException("Invite is invalid or expired.");

        var userEmail = await ResolveUserEmailAsync(userId, cancellationToken);

        if (!string.IsNullOrWhiteSpace(invite.Email) && !string.Equals(invite.Email, userEmail, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Invite email does not match current user.");

        var existingMembership = await _memberRepository.GetMembershipAsync(userId, invite.OrganizationId, cancellationToken);

        if (existingMembership == null)
        {
            var member = new OrganizationMember
            {
                Id = Guid.NewGuid(),
                OrganizationId = invite.OrganizationId,
                UserId = userId,
                Email = userEmail,
                Role = invite.Role,
                JoinedAt = DateTime.UtcNow
            };
            _memberRepository.Add(member);
        }

        invite.UsedAt = DateTime.UtcNow;
        invite.UsedByUserId = userId;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new JoinOrganizationResponse
        {
            OrganizationId = invite.OrganizationId,
            OrganizationName = invite.Organization?.Name ?? string.Empty,
            Role = existingMembership?.Role ?? invite.Role,
            AlreadyMember = existingMembership != null
        };
    }

    public async Task<GenerateOrgJoinCodeResponse> GenerateJoinCodeAsync(
        Guid orgId,
        string userId,
        GenerateOrgJoinCodeRequest request,
        CancellationToken cancellationToken = default)
    {
        var callerMembership = await GetCallerMembershipAsync(orgId, userId, cancellationToken);
        if (callerMembership == null)
            throw new InvalidOperationException("You are not a member of this organization.");

        if (!CanManageJoin(callerMembership))
            throw new InvalidOperationException("Only the organization owner can manage the org join code.");

        if (!OrganizationMember.Roles.IsValid(request.Role))
            throw new ArgumentException($"Invalid role: {request.Role}.", nameof(request.Role));

        var activeCodes = await _organizationRepository.GetActiveJoinCodesAsync(orgId, cancellationToken);
        foreach (var activeCode in activeCodes)
        {
            activeCode.RevokedAt = DateTime.UtcNow;
        }

        var code = GenerateJoinCode();
        var joinCode = new OrganizationJoinCode
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            CodeHash = HashSecret(code),
            Role = request.Role,
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = request.ExpiresInDays.HasValue ? DateTime.UtcNow.AddDays(request.ExpiresInDays.Value) : null
        };

        _organizationRepository.AddJoinCode(joinCode);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new GenerateOrgJoinCodeResponse
        {
            JoinCodeId = joinCode.Id,
            OrganizationId = orgId,
            Code = code,
            Role = joinCode.Role,
            ExpiresAt = joinCode.ExpiresAt
        };
    }

    public async Task<JoinOrganizationResponse> JoinByCodeAsync(
        string userId,
        JoinOrgByCodeRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
            throw new ArgumentException("Join code is required.", nameof(request.Code));

        var normalizedCode = request.Code.Trim().ToUpperInvariant();
        var codeHash = HashSecret(normalizedCode);

        var joinCode = await _organizationRepository.GetJoinCodeByCodeHashAsync(codeHash, cancellationToken);

        if (joinCode == null || joinCode.RevokedAt != null || (joinCode.ExpiresAt.HasValue && joinCode.ExpiresAt.Value <= DateTime.UtcNow))
            throw new InvalidOperationException("Join code is invalid or expired.");

        var existingMembership = await _memberRepository.GetMembershipAsync(userId, joinCode.OrganizationId, cancellationToken);

        if (existingMembership != null)
        {
            return new JoinOrganizationResponse
            {
                OrganizationId = joinCode.OrganizationId,
                OrganizationName = joinCode.Organization?.Name ?? string.Empty,
                Role = existingMembership.Role,
                AlreadyMember = true
            };
        }

        var userEmail = await ResolveUserEmailAsync(userId, cancellationToken);

        var member = new OrganizationMember
        {
            Id = Guid.NewGuid(),
            OrganizationId = joinCode.OrganizationId,
            UserId = userId,
            Email = userEmail,
            Role = joinCode.Role,
            JoinedAt = DateTime.UtcNow
        };

        _memberRepository.Add(member);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new JoinOrganizationResponse
        {
            OrganizationId = joinCode.OrganizationId,
            OrganizationName = joinCode.Organization?.Name ?? string.Empty,
            Role = joinCode.Role,
            AlreadyMember = false
        };
    }

    public async Task<AdminOrgEmailSearchResponse> SearchOrganizationsByEmailAsync(
        string? search,
        CancellationToken cancellationToken = default)
    {
        // A blank search must never fall back to listing every organization on the platform --
        // Support Mode is a targeted "find the landlord who contacted us" lookup, not a directory.
        if (string.IsNullOrWhiteSpace(search))
            return new AdminOrgEmailSearchResponse();

        const int maxMatches = 50;
        var matchedMembers = await _memberRepository.SearchByEmailAsync(search.Trim(), maxMatches, cancellationToken);
        if (matchedMembers.Count == 0)
            return new AdminOrgEmailSearchResponse();

        var orgIds = matchedMembers.Select(m => m.OrganizationId).Distinct().ToList();
        var orgs = await _organizationRepository.GetByIdsAsync(orgIds, cancellationToken);
        var orgsById = orgs.ToDictionary(o => o.Id);

        var allMembersForOrgs = await _memberRepository.GetByOrganizationIdsAsync(orgIds, cancellationToken);
        var membersByOrg = allMembersForOrgs.GroupBy(m => m.OrganizationId).ToDictionary(g => g.Key, g => g.ToList());

        var matches = matchedMembers
            .GroupBy(m => m.Email!, StringComparer.OrdinalIgnoreCase)
            .Select(g => new AdminMemberSearchResultResponse
            {
                Email = g.Key,
                Organizations = g
                    .Where(m => orgsById.ContainsKey(m.OrganizationId))
                    .Select(m =>
                    {
                        var org = orgsById[m.OrganizationId];
                        membersByOrg.TryGetValue(org.Id, out var orgMembers);
                        orgMembers ??= new List<OrganizationMember>();
                        var owner = orgMembers.FirstOrDefault(om => om.IsPrimaryOwner) ?? orgMembers.FirstOrDefault();
                        return new AdminOrganizationSummaryResponse
                        {
                            Id = org.Id,
                            Name = org.Name,
                            CreatedAt = org.CreatedAt,
                            OwnerEmail = owner?.Email,
                            MemberCount = orgMembers.Count
                        };
                    })
                    .ToList()
            })
            .Where(r => r.Organizations.Count > 0)
            .ToList();

        return new AdminOrgEmailSearchResponse { Matches = matches };
    }

    /// <summary>
    /// Real membership if one exists; otherwise, if the caller is a platform admin with an active
    /// Support Session on this exact org (per ITenantContext -- already resolved once per request
    /// by TenantContextMiddleware, the same source OrgResourceAuthorizationHandler uses to grant
    /// controller-level access), a synthetic elevated membership so this service doesn't reject a
    /// controller-authorized support action as "not a member". IsPrimaryOwner is always false for
    /// the synthetic case: Support Session elevation grants Landlord-equivalent rights, never
    /// primary-owner-equivalent, so owner-only actions (rename/delete/manage join) still correctly
    /// require the real owner -- only the *error path* changes (403 "only the owner can..." instead
    /// of a misleading 404/"not a member" for an otherwise-authorized admin).
    /// </summary>
    private async Task<OrganizationMember?> GetCallerMembershipAsync(Guid orgId, string userId, CancellationToken cancellationToken)
    {
        var membership = await _memberRepository.GetMembershipAsync(userId, orgId, cancellationToken);
        if (membership != null)
            return membership;

        if (_tenantContext.CurrentOrgId == orgId && _tenantContext.ActiveSupportSessionId.HasValue)
        {
            return new OrganizationMember
            {
                OrganizationId = orgId,
                UserId = userId,
                Email = null,
                Role = _tenantContext.CurrentRole ?? OrganizationMember.Roles.Landlord,
                IsPrimaryOwner = false
            };
        }

        return null;
    }

    private static OrganizationResponse MapToResponse(Organization org, string? currentUserRole = null)
        => new()
        {
            Id = org.Id,
            Name = org.Name,
            CreatedAt = org.CreatedAt,
            CurrentUserRole = currentUserRole
        };

    private static OrganizationMemberResponse MapToResponse(OrganizationMember member)
        => new()
        {
            Id = member.Id,
            UserId = member.UserId,
            Email = member.Email,
            DisplayName = null,
            Role = member.Role,
            IsPrimaryOwner = member.IsPrimaryOwner,
            JoinedAt = member.JoinedAt
        };

    private static OrganizationMemberResponse MapToResponseWithProfile(OrganizationMember member, AppUserProfile? profile)
    {
        var email = PreferProfileEmail(member.Email, profile?.Email);
        var displayName = profile?.DisplayName;
        var displayEmail = !string.IsNullOrWhiteSpace(email) ? email : null;
        var displayDisplayName = !string.IsNullOrWhiteSpace(displayName) ? displayName : displayEmail;
        return new OrganizationMemberResponse
        {
            Id = member.Id,
            UserId = member.UserId,
            Email = displayEmail ?? "Signed-in user",
            DisplayName = displayDisplayName,
            Role = member.Role,
            IsPrimaryOwner = member.IsPrimaryOwner,
            JoinedAt = member.JoinedAt
        };
    }

    /// <summary>Use profile email when member email is missing or is a placeholder (e.g. dev@local).</summary>
    private static string? PreferProfileEmail(string? memberEmail, string? profileEmail)
    {
        if (!string.IsNullOrWhiteSpace(profileEmail) && (string.IsNullOrWhiteSpace(memberEmail) || memberEmail.EndsWith("@local", StringComparison.OrdinalIgnoreCase)))
            return profileEmail;
        return !string.IsNullOrWhiteSpace(memberEmail) ? memberEmail : profileEmail;
    }

    // "Admin" org-role no longer exists (Admin is now platform-level, see AppUser.IsPlatformAdmin).
    // Conservatively keep this gated to the primary owner; phase 18 revisits with the real
    // permission system if co-managing Landlords should be able to invite too.
    private static bool CanManageJoin(OrganizationMember membership) => membership.IsPrimaryOwner;

    /// <summary>
    /// Resolve current user's email from AppUser (local auth) or AppUserProfile (OIDC/Dev).
    /// </summary>
    private async Task<string> ResolveUserEmailAsync(string userId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new InvalidOperationException("User context is invalid.");

        if (Guid.TryParse(userId, out var parsedUserId))
        {
            var appUser = await _appUserRepository.GetByIdAsync(parsedUserId, cancellationToken);
            if (appUser != null)
                return appUser.Email;
        }

        var profile = await _appUserProfileRepository.GetByIdAsync(userId, cancellationToken);
        if (profile != null)
            return profile.Email;

        throw new InvalidOperationException("User account not found.");
    }

    private static string GenerateSecureToken()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static string GenerateJoinCode()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        Span<byte> bytes = stackalloc byte[8];
        RandomNumberGenerator.Fill(bytes);
        var chars = new char[8];
        for (var i = 0; i < chars.Length; i++)
        {
            chars[i] = alphabet[bytes[i] % alphabet.Length];
        }
        return new string(chars);
    }

    private static string HashSecret(string input)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash);
    }
}
