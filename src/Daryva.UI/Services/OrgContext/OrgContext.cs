using System.IO;
using System.Text.Json;
using Daryva.Services;
using Daryva.Services.Api;
using Daryva.Services.Platform;
using Daryva.Services.Session;
using Microsoft.Extensions.DependencyInjection;

namespace Daryva.Services.OrgContext;

/// <summary>
/// Org context backed by GET /api/me. Persists CurrentOrgId per user via IScopedStorage.
/// Organisation is the root aggregate: app must not enter dashboard without a valid CurrentOrgId.
/// </summary>
public sealed class OrgContext : IOrgContext
{
    private const string ScopedStorageCurrentOrgKey = "currentOrgId";
    private const string CurrentOrgFileName = "current_org.json";

    private readonly IServiceProvider _serviceProvider;
    private readonly IConfigurationService _configuration;
    private readonly IAuthSessionService _authSession;
    private readonly IAppPaths _appPaths;
    private readonly ISessionContext _sessionContext;
    private readonly IScopedStorage _scopedStorage;

    private List<OrgSummary> _orgs = new();
    private Guid? _currentOrgId;
    /// <summary>Current user id from /api/me; used to detect user change and clear org selection.</summary>
    private string? _currentUserId;

    public Guid? CurrentOrgId => _currentOrgId;
    public IReadOnlyList<OrgSummary> Orgs => _orgs;
    public OrgSummary? CurrentOrg => _currentOrgId.HasValue ? _orgs.FirstOrDefault(o => o.Id == _currentOrgId.Value) : null;
    public bool RequiresOnboarding { get; private set; }
    public bool RequiresProfile { get; private set; }
    public bool IsPlatformAdmin { get; private set; }

    public event EventHandler<CurrentOrgChangedEventArgs>? CurrentOrgChanged;
    public event EventHandler? CurrentOrgDetailsChanged;

    public OrgContext(
        IServiceProvider serviceProvider,
        IConfigurationService configuration,
        IAuthSessionService authSession,
        IAppPaths appPaths,
        ISessionContext sessionContext,
        IScopedStorage scopedStorage)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _authSession = authSession ?? throw new ArgumentNullException(nameof(authSession));
        _appPaths = appPaths ?? throw new ArgumentNullException(nameof(appPaths));
        _sessionContext = sessionContext ?? throw new ArgumentNullException(nameof(sessionContext));
        _scopedStorage = scopedStorage ?? throw new ArgumentNullException(nameof(scopedStorage));
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (!_sessionContext.IsAuthenticated)
        {
            _orgs.Clear();
            _currentOrgId = null;
            _currentUserId = null;
            IsPlatformAdmin = false;
            return;
        }

        using var scope = _serviceProvider.CreateScope();
        var meApiService = scope.ServiceProvider.GetRequiredService<IMeApiService>();

        var me = await meApiService.GetMeAsync(cancellationToken).ConfigureAwait(false);

        if (me == null)
        {
            _orgs.Clear();
            _currentOrgId = null;
            _currentUserId = _authSession.UserId;
            RequiresProfile = false;
            RequiresOnboarding = true;
            IsPlatformAdmin = false;
            return;
        }

        var previousUserId = _currentUserId;
        _currentUserId = me.User?.Id;
        _sessionContext.UpdateFromMe(me.User?.Id, me.User?.Email);
        RequiresOnboarding = me.RequiresOrgSetup;
        RequiresProfile = me.RequiresProfileSetup;
        // Set before the org-count early return below: an admin needs no org memberships of their
        // own at all -- that's the whole point of Support Mode -- so this must not depend on _orgs.
        IsPlatformAdmin = me.User?.IsPlatformAdmin ?? false;

        _orgs = (me.Organisations ?? new List<MeOrganisationDto>())
            .Select(o => new OrgSummary { Id = o.Id, Name = o.Name, Role = o.CurrentUserRole ?? "Member" })
            .ToList();

        System.Diagnostics.Debug.WriteLine($"[OrgContext] /api/me: Orgs count={_orgs.Count}, UserId present={!string.IsNullOrEmpty(_currentUserId)}");

        if (previousUserId != null && previousUserId != _currentUserId)
            _currentOrgId = null;

        if (_orgs.Count == 0)
        {
            _currentOrgId = null;
            _scopedStorage.Remove(ScopedStorageCurrentOrgKey);
            return;
        }

        ApplyPreferredOrFirstOrg();
    }

    public void NotifyCurrentOrgDetailsChanged() => CurrentOrgDetailsChanged?.Invoke(this, EventArgs.Empty);

    public void ClearCurrentOrgSelection()
    {
        _currentOrgId = null;
        ClearPersistedCurrentOrg();
    }

    public void ClearForSignOut()
    {
        _orgs.Clear();
        _currentOrgId = null;
        _currentUserId = null;
        IsPlatformAdmin = false;
        ClearPersistedCurrentOrg();
    }

    private void ApplyPreferredOrFirstOrg()
    {
        var preferredId = ReadPersistedCurrentOrgId();
        var preferred = preferredId.HasValue ? _orgs.FirstOrDefault(o => o.Id == preferredId.Value) : null;

        if (preferred != null)
        {
            _currentOrgId = preferred.Id;
            SavePersistedCurrentOrg(preferred.Id);
            System.Diagnostics.Debug.WriteLine($"[OrgContext] CurrentOrgId after selection: {_currentOrgId} (from scoped storage)");
        }
        else if (_orgs.Count == 1)
        {
            _currentOrgId = _orgs[0].Id;
            SavePersistedCurrentOrg(_orgs[0].Id);
            System.Diagnostics.Debug.WriteLine($"[OrgContext] CurrentOrgId after selection: {_currentOrgId} (single org)");
        }
        else
        {
            _currentOrgId = null;
            System.Diagnostics.Debug.WriteLine("[OrgContext] CurrentOrgId=null (Choose Org)");
        }
    }

    private Guid? ReadPersistedCurrentOrgId()
    {
        var fromScoped = _scopedStorage.Get(ScopedStorageCurrentOrgKey);
        if (!string.IsNullOrEmpty(fromScoped) && Guid.TryParse(fromScoped, out var id) && id != Guid.Empty)
            return id;
        var fromFile = TryReadCurrentOrgIdFromFile();
        if (fromFile.HasValue)
        {
            _scopedStorage.Set(ScopedStorageCurrentOrgKey, fromFile.Value.ToString());
            return fromFile;
        }
        return null;
    }

    private Guid? TryReadCurrentOrgIdFromFile()
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
        _scopedStorage.Set(ScopedStorageCurrentOrgKey, orgId.ToString());
        try
        {
            var path = Path.Combine(_appPaths.AppData, CurrentOrgFileName);
            var userId = _currentUserId ?? _authSession.UserId;
            if (!string.IsNullOrEmpty(userId))
            {
                var json = JsonSerializer.Serialize(new { userId, currentOrgId = orgId.ToString() });
                File.WriteAllText(path, json);
            }
        }
        catch { /* ignore */ }
    }

    private void ClearPersistedCurrentOrg()
    {
        _scopedStorage.Remove(ScopedStorageCurrentOrgKey);
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
        SavePersistedCurrentOrg(orgId);
        CurrentOrgChanged?.Invoke(this, new CurrentOrgChangedEventArgs { NewOrgId = orgId });
        return Task.CompletedTask;
    }

    public Task SetCurrentOrgFromRecoveryAsync(Guid orgId, string name, CancellationToken cancellationToken = default)
    {
        if (!_orgs.Any(o => o.Id == orgId))
            _orgs.Add(new OrgSummary { Id = orgId, Name = name ?? "Organisation", Role = "Member" });
        _currentOrgId = orgId;
        SavePersistedCurrentOrg(orgId);
        CurrentOrgChanged?.Invoke(this, new CurrentOrgChangedEventArgs { NewOrgId = orgId });
        return Task.CompletedTask;
    }

    public Task EnterSupportOrgAsync(Guid orgId, string orgName, CancellationToken cancellationToken = default)
    {
        if (!IsPlatformAdmin)
            return Task.CompletedTask;
        _orgs.RemoveAll(o => o.Id == orgId);
        _orgs.Add(new OrgSummary { Id = orgId, Name = orgName, Role = OrgSummary.SupportSessionRole });
        _currentOrgId = orgId;
        // Deliberately NOT persisted via SavePersistedCurrentOrg -- see interface doc comment.
        CurrentOrgChanged?.Invoke(this, new CurrentOrgChangedEventArgs { NewOrgId = orgId });
        return Task.CompletedTask;
    }
}
