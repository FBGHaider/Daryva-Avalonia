using Daryva.Api.Data;
using Daryva.Api.Domain;
using Daryva.Api.Dtos;
using Daryva.Api.Security;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace Daryva.Api.Services;

/// <summary>
/// Business logic for organization management and multi-tenancy.
/// </summary>
public interface IOrganizationService
{
    /// <summary>
    /// Create a new organization. Current user becomes the Owner.
    /// </summary>
    Task<OrganizationResponse> CreateOrganizationAsync(
        string userId,
        CreateOrganizationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all organizations the user belongs to.
    /// </summary>
    Task<IEnumerable<OrganizationResponse>> GetUserOrganizationsAsync(
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get organization by ID (if user is member).
    /// </summary>
    Task<OrganizationResponse?> GetOrganizationAsync(
        Guid orgId,
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Add a member to an organization (by email).
    /// </summary>
    Task<OrganizationMemberResponse> AddMemberAsync(
        Guid orgId,
        string userId,
        AddMemberRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete an organization (Owner only).
    /// </summary>
    Task<bool> DeleteOrganizationAsync(
        Guid orgId,
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all members of an organization (if user is member).
    /// </summary>
    Task<IEnumerable<OrganizationMemberResponse>> GetOrganizationMembersAsync(
        Guid orgId,
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a single-use invite token for joining an organization.
    /// </summary>
    Task<CreateOrgInviteResponse> CreateInviteAsync(
        Guid orgId,
        string userId,
        CreateOrgInviteRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Accept an invite token and join the referenced organization.
    /// </summary>
    Task<JoinOrganizationResponse> AcceptInviteAsync(
        string userId,
        AcceptOrgInviteRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate (and rotate) a reusable org join code.
    /// </summary>
    Task<GenerateOrgJoinCodeResponse> GenerateJoinCodeAsync(
        Guid orgId,
        string userId,
        GenerateOrgJoinCodeRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Join an organization using an active join code.
    /// </summary>
    Task<JoinOrganizationResponse> JoinByCodeAsync(
        string userId,
        JoinOrgByCodeRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of IOrganizationService.
/// </summary>
public class OrganizationService : IOrganizationService
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<OrganizationService> _logger;

    public OrganizationService(AppDbContext dbContext, ILogger<OrganizationService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<OrganizationResponse> CreateOrganizationAsync(
        string userId,
        CreateOrganizationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("Organization name cannot be empty.", nameof(request.Name));

        var org = new Organization
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        // Add current user as Owner
        var member = new OrganizationMember
        {
            Id = Guid.NewGuid(),
            OrganizationId = org.Id,
            UserId = userId,
            Role = OrganizationMember.Roles.Owner,
            JoinedAt = DateTime.UtcNow
        };

        _dbContext.Organizations.Add(org);
        _dbContext.OrganizationMembers.Add(member);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created organization {OrgId} with owner {UserId}.", org.Id, userId);

        return MapToResponse(org, OrganizationMember.Roles.Owner);
    }

    public async Task<IEnumerable<OrganizationResponse>> GetUserOrganizationsAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var memberships = await _dbContext.OrganizationMembers
            .Where(m => m.UserId == userId)
            .Include(m => m.Organization)
            .ToListAsync(cancellationToken);

        return memberships
            .Where(m => m.Organization != null)
            .Select(m => MapToResponse(m.Organization!, m.Role))
            .ToList();
    }

    public async Task<OrganizationResponse?> GetOrganizationAsync(
        Guid orgId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        // Verify user is member
        var membership = await _dbContext.OrganizationMembers
            .FirstOrDefaultAsync(m => m.OrganizationId == orgId && m.UserId == userId, cancellationToken);

        if (membership == null)
            return null;

        var org = await _dbContext.Organizations
            .FirstOrDefaultAsync(o => o.Id == orgId, cancellationToken);

        return org == null ? null : MapToResponse(org, membership.Role);
    }

    public async Task<OrganizationMemberResponse> AddMemberAsync(
        Guid orgId,
        string userId,
        AddMemberRequest request,
        CancellationToken cancellationToken = default)
    {
        // Verify caller is member of the org (could add role check for Admin/Owner only)
        var callerMembership = await _dbContext.OrganizationMembers
            .FirstOrDefaultAsync(m => m.OrganizationId == orgId && m.UserId == userId, cancellationToken);

        if (callerMembership == null)
            throw new InvalidOperationException("You are not a member of this organization.");

        // Validate role
        if (!OrganizationMember.Roles.IsValid(request.Role))
            throw new ArgumentException($"Invalid role: {request.Role}.", nameof(request.Role));

        // Check if member already exists by email
        var existingMember = await _dbContext.OrganizationMembers
            .FirstOrDefaultAsync(m => m.OrganizationId == orgId && m.Email == request.Email, cancellationToken);

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

        _dbContext.OrganizationMembers.Add(newMember);
        await _dbContext.SaveChangesAsync(cancellationToken);

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
        var membership = await _dbContext.OrganizationMembers
            .FirstOrDefaultAsync(m => m.OrganizationId == orgId && m.UserId == userId, cancellationToken);

        if (membership == null)
            return false;

        if (!string.Equals(membership.Role, OrganizationMember.Roles.Owner, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only organization owners can delete an organization.");

        var org = await _dbContext.Organizations
            .FirstOrDefaultAsync(o => o.Id == orgId, cancellationToken);

        if (org == null)
            return false;

        _dbContext.Organizations.Remove(org);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Organization {OrgId} deleted by user {UserId}.", orgId, userId);
        return true;
    }

    public async Task<IEnumerable<OrganizationMemberResponse>> GetOrganizationMembersAsync(
        Guid orgId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        // Verify user is member
        var membership = await _dbContext.OrganizationMembers
            .FirstOrDefaultAsync(m => m.OrganizationId == orgId && m.UserId == userId, cancellationToken);

        if (membership == null)
            return Enumerable.Empty<OrganizationMemberResponse>();

        var members = await _dbContext.OrganizationMembers
            .Where(m => m.OrganizationId == orgId)
            .ToListAsync(cancellationToken);

        return members.Select(MapToResponse).ToList();
    }

    public async Task<CreateOrgInviteResponse> CreateInviteAsync(
        Guid orgId,
        string userId,
        CreateOrgInviteRequest request,
        CancellationToken cancellationToken = default)
    {
        var callerMembership = await _dbContext.OrganizationMembers
            .FirstOrDefaultAsync(m => m.OrganizationId == orgId && m.UserId == userId, cancellationToken);

        if (callerMembership == null)
            throw new InvalidOperationException("You are not a member of this organization.");

        if (!CanManageJoin(callerMembership.Role))
            throw new InvalidOperationException("Only Owner/Admin can create invites.");

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

        _dbContext.OrganizationInvites.Add(invite);
        await _dbContext.SaveChangesAsync(cancellationToken);

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
        var invite = await _dbContext.OrganizationInvites
            .Include(i => i.Organization)
            .FirstOrDefaultAsync(i => i.TokenHash == tokenHash, cancellationToken);

        if (invite == null || invite.RevokedAt != null || invite.UsedAt != null || invite.ExpiresAt <= DateTime.UtcNow)
            throw new InvalidOperationException("Invite is invalid or expired.");

        var appUser = await ResolveAppUserAsync(userId, cancellationToken);

        if (!string.IsNullOrWhiteSpace(invite.Email) && !string.Equals(invite.Email, appUser.Email, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Invite email does not match current user.");

        var existingMembership = await _dbContext.OrganizationMembers
            .FirstOrDefaultAsync(m => m.OrganizationId == invite.OrganizationId && m.UserId == userId, cancellationToken);

        if (existingMembership == null)
        {
            var member = new OrganizationMember
            {
                Id = Guid.NewGuid(),
                OrganizationId = invite.OrganizationId,
                UserId = userId,
                Email = appUser.Email,
                Role = invite.Role,
                JoinedAt = DateTime.UtcNow
            };
            _dbContext.OrganizationMembers.Add(member);
        }

        invite.UsedAt = DateTime.UtcNow;
        invite.UsedByUserId = userId;

        await _dbContext.SaveChangesAsync(cancellationToken);

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
        var callerMembership = await _dbContext.OrganizationMembers
            .FirstOrDefaultAsync(m => m.OrganizationId == orgId && m.UserId == userId, cancellationToken);

        if (callerMembership == null)
            throw new InvalidOperationException("You are not a member of this organization.");

        if (!CanManageJoin(callerMembership.Role))
            throw new InvalidOperationException("Only Owner/Admin can manage org join code.");

        if (!OrganizationMember.Roles.IsValid(request.Role))
            throw new ArgumentException($"Invalid role: {request.Role}.", nameof(request.Role));

        var activeCodes = await _dbContext.OrganizationJoinCodes
            .Where(c => c.OrganizationId == orgId && c.RevokedAt == null)
            .ToListAsync(cancellationToken);

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

        _dbContext.OrganizationJoinCodes.Add(joinCode);
        await _dbContext.SaveChangesAsync(cancellationToken);

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

        var joinCode = await _dbContext.OrganizationJoinCodes
            .Include(c => c.Organization)
            .FirstOrDefaultAsync(c => c.CodeHash == codeHash, cancellationToken);

        if (joinCode == null || joinCode.RevokedAt != null || (joinCode.ExpiresAt.HasValue && joinCode.ExpiresAt.Value <= DateTime.UtcNow))
            throw new InvalidOperationException("Join code is invalid or expired.");

        var existingMembership = await _dbContext.OrganizationMembers
            .FirstOrDefaultAsync(m => m.OrganizationId == joinCode.OrganizationId && m.UserId == userId, cancellationToken);

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

        var appUser = await ResolveAppUserAsync(userId, cancellationToken);

        var member = new OrganizationMember
        {
            Id = Guid.NewGuid(),
            OrganizationId = joinCode.OrganizationId,
            UserId = userId,
            Email = appUser.Email,
            Role = joinCode.Role,
            JoinedAt = DateTime.UtcNow
        };

        _dbContext.OrganizationMembers.Add(member);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new JoinOrganizationResponse
        {
            OrganizationId = joinCode.OrganizationId,
            OrganizationName = joinCode.Organization?.Name ?? string.Empty,
            Role = joinCode.Role,
            AlreadyMember = false
        };
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
            Role = member.Role,
            JoinedAt = member.JoinedAt
        };

    private static bool CanManageJoin(string role) =>
        string.Equals(role, OrganizationMember.Roles.Owner, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(role, OrganizationMember.Roles.Admin, StringComparison.OrdinalIgnoreCase);

    private async Task<AppUser> ResolveAppUserAsync(string userId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(userId, out var parsedUserId))
            throw new InvalidOperationException("User context is invalid.");

        var appUser = await _dbContext.AppUsers.FirstOrDefaultAsync(u => u.Id == parsedUserId, cancellationToken);
        if (appUser == null)
            throw new InvalidOperationException("User account not found.");

        return appUser;
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
