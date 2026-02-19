namespace Daryva.Services.Api;

/// <summary>
/// Extension methods for IOrganizationApiService to simplify org management.
/// </summary>
public static class OrganizationApiServiceExtensions
{
    /// <summary>
    /// Get or create the user's default organization.
    /// If user has no organizations, creates one with the given name.
    /// Returns the first/only organization the user belongs to.
    /// </summary>
    public static async Task<OrganizationDto?> GetOrCreateDefaultOrgAsync(
        this IOrganizationApiService organizationService,
        string defaultOrgName = "Default Organization")
    {
        var userOrgs = await organizationService.GetUserOrganizationsAsync();
        
        if (userOrgs.Count > 0)
        {
            return userOrgs[0];
        }

        // User has no organizations, create default one
        var createResult = await organizationService.CreateOrganizationAsync(defaultOrgName);
        
        return createResult;
    }

    /// <summary>
    /// Ensure the API client is using the given organization.
    /// This sets the X-Org-Id header for all subsequent requests.
    /// </summary>
    public static async Task SetApiContextOrgAsync(
        this IApiClient apiClient,
        IOrganizationApiService organizationService,
        Guid? organizationId = null)
    {
        if (organizationId.HasValue && organizationId != Guid.Empty)
        {
            apiClient.SetCurrentOrgId(organizationId.Value);
            return;
        }

        // Auto-select organization
        var org = await organizationService.GetOrCreateDefaultOrgAsync();
        if (org != null)
        {
            apiClient.SetCurrentOrgId(org.Id);
        }
        else
        {
            throw new InvalidOperationException(
                "Cannot determine organization context. User has no organizations and cannot create one.");
        }
    }
}
