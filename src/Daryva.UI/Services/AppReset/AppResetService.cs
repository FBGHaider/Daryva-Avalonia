using Daryva.Services;
using Daryva.Services.Api;
using Daryva.Services.Business;
using Daryva.Services.Navigation;
using Daryva.Services.OrgContext;
using Daryva.Services.Platform;
using Daryva.Services.Session;

namespace Daryva.Services.AppReset;

/// <summary>
/// Resets app to signed-out state: clears session, org context, API client, navigation stack, caches, and local files.
/// </summary>
public sealed class AppResetService : IAppResetService
{
    private const string OrgsFileName = "orgs.json";
    private const string CurrentOrgFileName = "current_org.json";
    private const string ApiCurrentOrgIdKey = "ApiCurrentOrgId";
    private const string MembersFileName = "org_members.json";

    private readonly ISessionContext _sessionContext;
    private readonly IOrgContext _orgContext;
    private readonly IApiClient _apiClient;
    private readonly INavigationService _navigationService;
    private readonly INotificationFeedService _notificationFeedService;
    private readonly IAppPaths _appPaths;
    private readonly IConfigurationService _configuration;

    public AppResetService(
        ISessionContext sessionContext,
        IOrgContext orgContext,
        IApiClient apiClient,
        INavigationService navigationService,
        INotificationFeedService notificationFeedService,
        IAppPaths appPaths,
        IConfigurationService configuration)
    {
        _sessionContext = sessionContext ?? throw new ArgumentNullException(nameof(sessionContext));
        _orgContext = orgContext ?? throw new ArgumentNullException(nameof(orgContext));
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
        _notificationFeedService = notificationFeedService ?? throw new ArgumentNullException(nameof(notificationFeedService));
        _appPaths = appPaths ?? throw new ArgumentNullException(nameof(appPaths));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public Task ResetToSignedOutAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _sessionContext.Clear();
            _orgContext.ClearForSignOut();
            _apiClient.ApplyAuthState();
            _navigationService.ClearStackAndCurrent();
            _notificationFeedService.ClearForSignOut();
        }
        catch
        {
            // Best effort; continue to clear files
        }

        var dir = _appPaths.AppData;
        if (!string.IsNullOrEmpty(dir))
        {
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
                // Best effort
            }
        }

        return Task.CompletedTask;
    }

    public Task ResetToSignedInSkeletonAsync(CancellationToken cancellationToken = default)
    {
        // Optional: e.g. clear navigation to known state before loading /api/me. No-op for now.
        return Task.CompletedTask;
    }
}
