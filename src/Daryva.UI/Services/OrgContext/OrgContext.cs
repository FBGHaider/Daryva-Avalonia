using Daryva.Services;
using Daryva.Services.Api;
using Microsoft.Extensions.DependencyInjection;

namespace Daryva.Services.OrgContext;

/// <summary>
/// Org context backed by GET /api/me. Persists CurrentOrgId via IApiClient/config.
/// </summary>
public sealed class OrgContext : IOrgContext
{
    private const string ApiCurrentOrgIdKey = "ApiCurrentOrgId";

    private readonly IServiceProvider _serviceProvider;
    private readonly IApiClient _apiClient;
    private readonly IConfigurationService _configuration;

    private List<OrgSummary> _orgs = new();
    private Guid? _currentOrgId;

    public Guid? CurrentOrgId => _currentOrgId;
    public IReadOnlyList<OrgSummary> Orgs => _orgs;
    public OrgSummary? CurrentOrg => _currentOrgId.HasValue ? _orgs.FirstOrDefault(o => o.Id == _currentOrgId.Value) : null;
    public bool RequiresOnboarding { get; private set; }
    public bool RequiresProfile { get; private set; }

    public event EventHandler<CurrentOrgChangedEventArgs>? CurrentOrgChanged;

    public OrgContext(IServiceProvider serviceProvider, IApiClient apiClient, IConfigurationService configuration)
    {
        _serviceProvider = serviceProvider;
        _apiClient = apiClient;
        _configuration = configuration;

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
                _configuration.SetLocalValue(ApiCurrentOrgIdKey, string.Empty);
                return;
            }
            ApplyPreferredOrFirstOrg();
            return;
        }

        RequiresOnboarding = me.RequiresOrgSetup;
        RequiresProfile = me.RequiresProfileSetup;

        _orgs = me.Organisations
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
                _configuration.SetLocalValue(ApiCurrentOrgIdKey, string.Empty);
                return;
            }
        }

        ApplyPreferredOrFirstOrg();
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
        var raw = _configuration.GetValue(ApiCurrentOrgIdKey);
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
        var preferredRaw = _configuration.GetValue(ApiCurrentOrgIdKey);
        var preferredId = Guid.TryParse(preferredRaw, out var p) && p != Guid.Empty ? p : (Guid?)null;
        var preferred = preferredId.HasValue ? _orgs.FirstOrDefault(o => o.Id == preferredId.Value) : null;

        if (preferred != null)
        {
            _currentOrgId = preferred.Id;
            _apiClient.SetCurrentOrgId(preferred.Id);
        }
        else if (_orgs.Count == 1)
        {
            _currentOrgId = _orgs[0].Id;
            _apiClient.SetCurrentOrgId(_orgs[0].Id);
            _configuration.SetLocalValue(ApiCurrentOrgIdKey, _orgs[0].Id.ToString());
        }
        else if (_currentOrgId.HasValue && _orgs.Any(o => o.Id == _currentOrgId.Value))
        {
            _apiClient.SetCurrentOrgId(_currentOrgId.Value);
        }
        else
        {
            _currentOrgId = _orgs[0].Id;
            _apiClient.SetCurrentOrgId(_orgs[0].Id);
            _configuration.SetLocalValue(ApiCurrentOrgIdKey, _orgs[0].Id.ToString());
        }
    }

    public Task SetCurrentOrgAsync(Guid orgId, CancellationToken cancellationToken = default)
    {
        if (!_orgs.Any(o => o.Id == orgId))
            return Task.CompletedTask;
        _currentOrgId = orgId;
        _apiClient.SetCurrentOrgId(orgId);
        _configuration.SetLocalValue(ApiCurrentOrgIdKey, orgId.ToString());
        CurrentOrgChanged?.Invoke(this, new CurrentOrgChangedEventArgs { NewOrgId = orgId });
        return Task.CompletedTask;
    }

    public Task SetCurrentOrgFromRecoveryAsync(Guid orgId, string name, CancellationToken cancellationToken = default)
    {
        if (!_orgs.Any(o => o.Id == orgId))
            _orgs.Add(new OrgSummary { Id = orgId, Name = name ?? "Organisation", Role = "Member" });
        _currentOrgId = orgId;
        _apiClient.SetCurrentOrgId(orgId);
        _configuration.SetLocalValue(ApiCurrentOrgIdKey, orgId.ToString());
        CurrentOrgChanged?.Invoke(this, new CurrentOrgChangedEventArgs { NewOrgId = orgId });
        return Task.CompletedTask;
    }
}
