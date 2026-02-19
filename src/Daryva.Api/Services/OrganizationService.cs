using Daryva.Api.Data;
using Daryva.Api.Domain;
using Daryva.Api.Dtos;
using Daryva.Api.Security;
using Microsoft.EntityFrameworkCore;

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
}
