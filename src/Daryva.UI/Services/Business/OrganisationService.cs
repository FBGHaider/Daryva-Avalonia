using System.Text.Json;
using Daryva.MVVM.Models;
using Daryva.Services.Api;
using Daryva.Services.Platform;

namespace Daryva.Services.Business
{
    /// <summary>
    /// Local JSON-backed organisation service. Syncs current org to IApiClient for app-wide consistency.
    /// </summary>
    public class OrganisationService : IOrganisationService
    {
        private const string OrgsFileName = "orgs.json";
        private const string CurrentOrgFileName = "current_org.json";
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        };

        private readonly IAppPaths _appPaths;
        private readonly IApiClient _apiClient;
        private readonly IAuthSessionService _authSession;
        private readonly IOrganisationMemberService _memberService;
        private readonly IOrganizationApiService? _organizationApiService;

        public OrganisationService(IAppPaths appPaths, IApiClient apiClient, IAuthSessionService authSession, IOrganisationMemberService memberService, IOrganizationApiService? organizationApiService = null)
        {
            _appPaths = appPaths ?? throw new ArgumentNullException(nameof(appPaths));
            _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
            _authSession = authSession ?? throw new ArgumentNullException(nameof(authSession));
            _memberService = memberService ?? throw new ArgumentNullException(nameof(memberService));
            _organizationApiService = organizationApiService;
        }

        public async Task<IReadOnlyList<Organisation>> GetMyOrganisationsAsync(CancellationToken cancellationToken = default)
        {
            var list = await ReadOrgsAsync(cancellationToken).ConfigureAwait(false);
            if (list.Count == 0)
            {
                await SeedDefaultOrganisationAsync(cancellationToken).ConfigureAwait(false);
                list = await ReadOrgsAsync(cancellationToken).ConfigureAwait(false);
            }
            if (_organizationApiService != null)
            {
                try
                {
                    var apiOrgs = await _organizationApiService.GetUserOrganizationsAsync(cancellationToken).ConfigureAwait(false);
                    var localIds = list.Select(o => o.Id).ToHashSet();
                    foreach (var dto in apiOrgs)
                    {
                        if (localIds.Add(dto.Id))
                            list.Insert(0, new Organisation { Id = dto.Id, Name = dto.Name, CreatedAt = dto.CreatedAt, PlanTier = null });
                    }
                }
                catch { /* API not available or not authenticated */ }
            }
            return list;
        }

        public async Task<Organisation> CreateOrganisationAsync(string name, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Organisation name is required.", nameof(name));
            var list = await ReadOrgsAsync(cancellationToken).ConfigureAwait(false);
            var org = new Organisation
            {
                Id = Guid.NewGuid(),
                Name = name.Trim(),
                CreatedAt = DateTime.UtcNow,
                PlanTier = "Starter"
            };
            list.Add(org);
            await WriteOrgsAsync(list, cancellationToken).ConfigureAwait(false);
            await SetCurrentOrganisationAsync(org.Id, cancellationToken).ConfigureAwait(false);
            return org;
        }

        public async Task RenameOrganisationAsync(Guid orgId, string newName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(newName)) throw new ArgumentException("Name is required.", nameof(newName));
            var list = await ReadOrgsAsync(cancellationToken).ConfigureAwait(false);
            var org = list.FirstOrDefault(o => o.Id == orgId);
            if (org == null) throw new InvalidOperationException("Organisation not found.");
            org.Name = newName.Trim();
            await WriteOrgsAsync(list, cancellationToken).ConfigureAwait(false);
        }

        public async Task SetCurrentOrganisationAsync(Guid orgId, CancellationToken cancellationToken = default)
        {
            // Allow switching to any org (local or API); list shown in UI can include API orgs not in orgs.json
            var path = Path.Combine(_appPaths.AppData, CurrentOrgFileName);
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(new CurrentOrgState { CurrentOrgId = orgId }, JsonOptions), cancellationToken).ConfigureAwait(false);
            _apiClient.SetCurrentOrgId(orgId);
        }

        public async Task<Guid?> GetCurrentOrganisationIdAsync(CancellationToken cancellationToken = default)
        {
            if (_apiClient.CurrentOrgId is { } apiOrgId && apiOrgId != Guid.Empty)
            {
                await PersistCurrentOrgAsync(apiOrgId, cancellationToken).ConfigureAwait(false);
                return apiOrgId;
            }
            var path = Path.Combine(_appPaths.AppData, CurrentOrgFileName);
            if (File.Exists(path))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
                    var state = JsonSerializer.Deserialize<CurrentOrgState>(json);
                    if (state?.CurrentOrgId is { } id && id != Guid.Empty) return id;
                }
                catch { /* fall through */ }
            }
            return null;
        }

        private async Task PersistCurrentOrgAsync(Guid orgId, CancellationToken cancellationToken)
        {
            var path = Path.Combine(_appPaths.AppData, CurrentOrgFileName);
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(new CurrentOrgState { CurrentOrgId = orgId }, JsonOptions), cancellationToken).ConfigureAwait(false);
        }

        public async Task<Organisation?> GetOrganisationAsync(Guid orgId, CancellationToken cancellationToken = default)
        {
            var list = await ReadOrgsAsync(cancellationToken).ConfigureAwait(false);
            return list.FirstOrDefault(o => o.Id == orgId);
        }

        private async Task<List<Organisation>> ReadOrgsAsync(CancellationToken cancellationToken)
        {
            var path = Path.Combine(_appPaths.AppData, OrgsFileName);
            if (!File.Exists(path)) return new List<Organisation>();
            try
            {
                var json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
                var list = JsonSerializer.Deserialize<List<Organisation>>(json, JsonOptions);
                return list ?? new List<Organisation>();
            }
            catch
            {
                return new List<Organisation>();
            }
        }

        private async Task WriteOrgsAsync(List<Organisation> list, CancellationToken cancellationToken)
        {
            var path = Path.Combine(_appPaths.AppData, OrgsFileName);
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(list, JsonOptions), cancellationToken).ConfigureAwait(false);
        }

        private async Task SeedDefaultOrganisationAsync(CancellationToken cancellationToken)
        {
            var list = await ReadOrgsAsync(cancellationToken).ConfigureAwait(false);
            if (list.Count > 0) return;
            var org = new Organisation
            {
                Id = Guid.NewGuid(),
                Name = "Daryva Organisation",
                CreatedAt = DateTime.UtcNow,
                PlanTier = "Starter"
            };
            list.Add(org);
            await WriteOrgsAsync(list, cancellationToken).ConfigureAwait(false);
            await SetCurrentOrganisationAsync(org.Id, cancellationToken).ConfigureAwait(false);
            var email = _authSession.Email ?? "user@local";
            try
            {
                await _memberService.AddMemberAsync(org.Id, email, OrgRole.Owner, MemberStatus.Active, null, cancellationToken).ConfigureAwait(false);
            }
            catch (InvalidOperationException)
            {
                // Already a member
            }
        }

        private class CurrentOrgState
        {
            public Guid CurrentOrgId { get; set; }
        }
    }
}
