namespace Daryva.Api.Services.Seed.Interfaces;

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
