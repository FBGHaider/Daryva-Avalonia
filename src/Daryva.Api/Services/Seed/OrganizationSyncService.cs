using Daryva.Api.Data;
using Daryva.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Daryva.Api.Services.Seed;

/// <summary>
/// Ensures the current dev user is a member of all organizations in the system.
/// This fixes the case where data was imported but the dev user can't access it.
/// </summary>
public interface IOrganizationSyncService
{
    /// <summary>
    /// Synchronize the current user's organization memberships with all existing organizations.
    /// If user is not a member of any organization that has data, adds them.
    /// </summary>
    Task SyncUserOrgMembershipsAsync(string userId);
}

public class OrganizationSyncService : IOrganizationSyncService
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<OrganizationSyncService> _logger;

    public OrganizationSyncService(AppDbContext dbContext, ILogger<OrganizationSyncService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task SyncUserOrgMembershipsAsync(string userId)
    {
        // Get all organizations that have data (tenants, houses, etc.)
        var orgsWithData = await _dbContext.Organizations
            .IgnoreQueryFilters()
            .Where(o => _dbContext.Tenants.IgnoreQueryFilters().Any(t => t.OrganizationId == o.Id) ||
                       _dbContext.Houses.IgnoreQueryFilters().Any(h => h.OrganizationId == o.Id))
            .ToListAsync();

        if (orgsWithData.Count == 0)
        {
            _logger.LogInformation("No organizations found with data, skipping sync.");
            return;
        }

        _logger.LogInformation("Found {OrgCount} organizations with data", orgsWithData.Count);

        // Get user's current memberships
        var userMemberships = await _dbContext.OrganizationMembers
            .IgnoreQueryFilters()
            .Where(m => m.UserId == userId)
            .Select(m => m.OrganizationId)
            .ToListAsync();

        _logger.LogInformation("User {UserId} is member of {MembershipCount} organizations", userId, userMemberships.Count);

        // Add user to any organization they're not yet a member of
        foreach (var org in orgsWithData)
        {
            if (!userMemberships.Contains(org.Id))
            {
                var membership = new OrganizationMember
                {
                    UserId = userId,
                    OrganizationId = org.Id,
                    Role = "owner",
                    JoinedAt = DateTime.UtcNow
                };

                _dbContext.OrganizationMembers.Add(membership);
                _logger.LogInformation("Added user {UserId} to organization {OrgId} ({OrgName})", 
                    userId, org.Id, org.Name);
            }
        }

        try
        {
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("Organization sync completed for user {UserId}", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing organization memberships for user {UserId}", userId);
        }
    }
}
