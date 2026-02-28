using System.IO;
using System.Text.Json;
using Daryva.Services;
using Daryva.Services.Api;
using Daryva.Services.Platform;
using Microsoft.Extensions.DependencyInjection;

namespace Daryva.Services.OrgContext;

/// <summary>
/// Org context backed by GET /api/me. Persists CurrentOrgId via config and AppData/current_org.json.
/// Organisation is the root aggregate: app must not enter dashboard without a valid CurrentOrgId.
/// </summary>
public sealed class OrgContext : IOrgContext
{
    private const string ApiCurrentOrgIdKey = "ApiCurrentOrgId";
    private const string CurrentOrgFileName = "current_org.json";

    private readonly IServiceProvider _serviceProvider;
    private readonly IApiClient _apiClient;
    private readonly IConfigurationService _configuration;
    private readonly IAuthSessionService _authSession;
    private readonly IAppPaths _appPaths;

    private List<OrgSummary> _orgs = new();
    private Guid? _currentOrgId;
    /// <summary>Current user id from /api/me; used to scope persisted current org per account.</summary>
    private string? _currentUserId;

    public Guid? CurrentOrgId => _currentOrgId;
    public IReadOnlyList<OrgSummary> Orgs => _orgs;
    public OrgSummary? CurrentOrg => _currentOrgId.HasValue ? _orgs.FirstOrDefault(o => o.Id == _currentOrgId.Value) : null;
    public bool RequiresOnboarding { get; private set; }
    public bool RequiresProfile { get; private set; }

    public event EventHandler<CurrentOrgChangedEventArgs>? CurrentOrgChanged;
    public event EventHandler? CurrentOrgDetailsChanged;

    private string GetCurrentOrgConfigKey() =>
        !string.IsNullOrEmpty(_currentUserId) ? ApiCurrentOrgIdKey + "_" + _currentUserId : ApiCurrentOrgIdKey;

    public OrgContext(
        IServiceProvider serviceProvider,
        IApiClient apiClient,
        IConfigurationService configuration,
        IAuthSessionService authSession,
        IAppPaths appPaths)
    {
        _serviceProvider = serviceProvider;
        _apiClient = apiClient;
        _configuration = configuration;
        _authSession = authSession ?? throw new ArgumentNullException(nameof(authSession));
        _appPaths = appPaths ?? throw new ArgumentNullException(nameof(appPaths));

        var raw = configuration.GetValue(ApiCurrentOrgIdKey);
        if (Guid.TryParse(raw, out var id) && id != Guid.Empty)
            _currentOrgId = id;
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _serviceProvider.CreateScope();
        var meApiService = scope.ServiceProvider.GetRequiredService<IMeApiService>();
        var orgApiService = scope.ServiceProvider.GetService<IOrganizationApiService>();

        var me = await meApiService.GetMeAsync(cancellationToken).ConfigureAwait(false);

        if (me == null)
        {
            _currentUserId = _authSession.UserId;
            // /api/me failed (e.g. 401, network). Try GET /api/orgs so we don't show setup when user has orgs.
            _orgs = await TryLoadOrgsFromApiAsync(orgApiService, cancellationToken).ConfigureAwait(false);
            if (_orgs.Count == 0)
                _orgs = await TryLoadPersistedOrgAsync(orgApiService, cancellationToken).ConfigureAwait(false);
            RequiresProfile = false;
            RequiresOnboarding = _orgs.Count == 0;
            if (_orgs.Count == 0)
            {
                _currentOrgId = null;
                _apiClient.ClearCurrentOrgId();
                ClearPersistedCurrentOrg();
                return;
            }
            ApplyPreferredOrFirstOrg();
            return;
        }

        _currentUserId = me.User?.Id;
        RequiresOnboarding = me.RequiresOrgSetup;
        RequiresProfile = me.RequiresProfileSetup;

        _orgs = (me.Organisations ?? new List<MeOrganisationDto>())
            .Select(o => new OrgSummary { Id = o.Id, Name = o.Name, Role = o.CurrentUserRole ?? "Member" })
            .ToList();

        if (_orgs.Count == 0)
        {
            var apiOrgs = await TryLoadOrgsFromApiAsync(orgApiService, cancellationToken).ConfigureAwait(false);
            if (apiOrgs.Count > 0)
            {
                _orgs = apiOrgs;
                RequiresOnboarding = false;
            }
            else
            {
                _orgs = await TryLoadPersistedOrgAsync(orgApiService, cancellationToken).ConfigureAwait(false);
                if (_orgs.Count > 0)
                    RequiresOnboarding = false;
            }

            if (_orgs.Count == 0)
            {
                _currentOrgId = null;
                _apiClient.ClearCurrentOrgId();
                ClearPersistedCurrentOrg();
                return;
            }
        }

        ApplyPreferredOrFirstOrg();
    }

    public void NotifyCurrentOrgDetailsChanged() => CurrentOrgDetailsChanged?.Invoke(this, EventArgs.Empty);

    public void ClearForSignOut()
    {
        _orgs.Clear();
        _currentOrgId = null;
        _currentUserId = null;
        _apiClient.ClearCurrentOrgId();
        ClearPersistedCurrentOrg();
    }

    private async Task<List<OrgSummary>> TryLoadOrgsFromApiAsync(IOrganizationApiService? orgApiService, CancellationToken cancellationToken)
    {
        if (orgApiService == null) return new List<OrgSummary>();
        try
        {
            var apiOrgs = await orgApiService.GetUserOrganizationsAsync(cancellationToken).ConfigureAwait(false);
            if (apiOrgs == null || apiOrgs.Count == 0) return new List<OrgSummary>();
            return apiOrgs
                .Select(o => new OrgSummary { Id = o.Id, Name = o.Name, Role = o.CurrentUserRole ?? "Member" })
                .ToList();
        }
        catch
        {
            return new List<OrgSummary>();
        }
    }

    private async Task<List<OrgSummary>> TryLoadPersistedOrgAsync(IOrganizationApiService? orgApiService, CancellationToken cancellationToken)
    {
        if (orgApiService == null) return new List<OrgSummary>();
        var raw = _configuration.GetValue(GetCurrentOrgConfigKey());
        if (!Guid.TryParse(raw, out var id) || id == Guid.Empty) return new List<OrgSummary>();
        try
        {
            var org = await orgApiService.GetOrganizationAsync(id, cancellationToken).ConfigureAwait(false);
            if (org == null) return new List<OrgSummary>();
            return new List<OrgSummary> { new OrgSummary { Id = org.Id, Name = org.Name, Role = org.CurrentUserRole ?? "Member" } };
        }
        catch
        {
            return new List<OrgSummary>();
        }
    }

    private void ApplyPreferredOrFirstOrg()
    {
        var key = GetCurrentOrgConfigKey();
        var preferredId = ReadPersistedCurrentOrgId();
        if (!preferredId.HasValue)
        {
            var preferredRaw = _configuration.GetValue(key);
            preferredId = Guid.TryParse(preferredRaw, out var p) && p != Guid.Empty ? p : (Guid?)null;
        }
        var preferred = preferredId.HasValue ? _orgs.FirstOrDefault(o => o.Id == preferredId.Value) : null;

        if (preferred != null)
        {
            _currentOrgId = preferred.Id;
            _apiClient.SetCurrentOrgId(preferred.Id);
            SavePersistedCurrentOrg(preferred.Id);
        }
        else if (_orgs.Count == 1)
        {
            _currentOrgId = _orgs[0].Id;
            _apiClient.SetCurrentOrgId(_orgs[0].Id);
            _configuration.SetLocalValue(key, _orgs[0].Id.ToString());
            SavePersistedCurrentOrg(_orgs[0].Id);
        }
        else if (_currentOrgId.HasValue && _orgs.Any(o => o.Id == _currentOrgId.Value))
        {
            _apiClient.SetCurrentOrgId(_currentOrgId.Value);
        }
        else
        {
            // Multiple orgs and no valid stored selection: leave CurrentOrgId null so app shows Choose Org screen.
            _currentOrgId = null;
            _apiClient.ClearCurrentOrgId();
        }
    }

    private Guid? ReadPersistedCurrentOrgId()
    {
        var userId = _currentUserId ?? _authSession.UserId;
        if (string.IsNullOrEmpty(userId)) return null;
        try
        {
            var path = Path.Combine(_appPaths.AppData, CurrentOrgFileName);
            if (!File.Exists(path)) return null;
            var json = File.ReadAllText(path);
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("userId", out var u) && u.GetString() == userId &&
                root.TryGetProperty("currentOrgId", out var o) && Guid.TryParse(o.GetString(), out var id) && id != Guid.Empty)
                return id;
        }
        catch { /* ignore */ }
        return null;
    }

    private void SavePersistedCurrentOrg(Guid orgId)
    {
        var userId = _currentUserId ?? _authSession.UserId;
        if (string.IsNullOrEmpty(userId)) return;
        try
        {
            var path = Path.Combine(_appPaths.AppData, CurrentOrgFileName);
            var json = JsonSerializer.Serialize(new { userId, currentOrgId = orgId.ToString() });
            File.WriteAllText(path, json);
        }
        catch { /* ignore */ }
        _configuration.SetLocalValue(GetCurrentOrgConfigKey(), orgId.ToString());
    }

    private void ClearPersistedCurrentOrg()
    {
        _configuration.SetLocalValue(GetCurrentOrgConfigKey(), string.Empty);
        try
        {
            var path = Path.Combine(_appPaths.AppData, CurrentOrgFileName);
            if (File.Exists(path))
                File.Delete(path);
        }
        catch { /* ignore */ }
    }

    public Task SetCurrentOrgAsync(Guid orgId, CancellationToken cancellationToken = default)
    {
        if (!_orgs.Any(o => o.Id == orgId))
            return Task.CompletedTask;
        _currentOrgId = orgId;
        _apiClient.SetCurrentOrgId(orgId);
        SavePersistedCurrentOrg(orgId);
        CurrentOrgChanged?.Invoke(this, new CurrentOrgChangedEventArgs { NewOrgId = orgId });
        return Task.CompletedTask;
    }

    public Task SetCurrentOrgFromRecoveryAsync(Guid orgId, string name, CancellationToken cancellationToken = default)
    {
        if (!_orgs.Any(o => o.Id == orgId))
            _orgs.Add(new OrgSummary { Id = orgId, Name = name ?? "Organisation", Role = "Member" });
        _currentOrgId = orgId;
        _apiClient.SetCurrentOrgId(orgId);
        SavePersistedCurrentOrg(orgId);
        CurrentOrgChanged?.Invoke(this, new CurrentOrgChangedEventArgs { NewOrgId = orgId });
        return Task.CompletedTask;
    }
}
