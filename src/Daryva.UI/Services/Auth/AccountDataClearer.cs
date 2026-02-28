using Daryva.Services.Api;
using Daryva.Services.OrgContext;
using Daryva.Services.Platform;

namespace Daryva.Services.Auth;

/// <summary>
/// Clears session, API client auth header, org context, and local files on sign-out so the next sign-in shows the correct account.
/// </summary>
public sealed class AccountDataClearer : IAccountDataClearer
{
    private const string OrgsFileName = "orgs.json";
    private const string CurrentOrgFileName = "current_org.json";
    private const string ApiCurrentOrgIdKey = "ApiCurrentOrgId";
    private const string MembersFileName = "org_members.json";

    private readonly IAppPaths _appPaths;
    private readonly IConfigurationService _configuration;
    private readonly IApiClient _apiClient;
    private readonly IOrgContext _orgContext;

    public AccountDataClearer(IAppPaths appPaths, IConfigurationService configuration, IApiClient apiClient, IOrgContext orgContext)
    {
        _appPaths = appPaths ?? throw new ArgumentNullException(nameof(appPaths));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        _orgContext = orgContext ?? throw new ArgumentNullException(nameof(orgContext));
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _orgContext.ClearForSignOut();
            _apiClient.ApplyAuthState();
        }
        catch
        {
            // Best effort
        }

        var dir = _appPaths.AppData;
        if (string.IsNullOrEmpty(dir))
            return Task.CompletedTask;

        try
        {
            var orgsPath = Path.Combine(dir, OrgsFileName);
            var currentOrgPath = Path.Combine(dir, CurrentOrgFileName);
            var membersPath = Path.Combine(dir, MembersFileName);

            if (File.Exists(orgsPath))
                File.WriteAllText(orgsPath, "[]");

            if (File.Exists(currentOrgPath))
                File.Delete(currentOrgPath);

            if (File.Exists(membersPath))
                File.WriteAllText(membersPath, "[]");

            _configuration.SetLocalValue(ApiCurrentOrgIdKey, string.Empty);
        }
        catch
        {
            // Best effort; do not throw so sign-out still completes
        }

        return Task.CompletedTask;
    }
}
