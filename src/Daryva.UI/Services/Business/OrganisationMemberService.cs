using System.Text.Json;
using Daryva.MVVM.Models;
using Daryva.Services.Platform;

namespace Daryva.Services.Business
{
    /// <summary>
    /// Local JSON-backed organisation member service.
    /// </summary>
    public class OrganisationMemberService : IOrganisationMemberService
    {
        public const string MembersFileName = "org_members.json";
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        };

        private readonly IAppPaths _appPaths;

        public OrganisationMemberService(IAppPaths appPaths)
        {
            _appPaths = appPaths ?? throw new ArgumentNullException(nameof(appPaths));
        }

        public async Task<IReadOnlyList<OrganisationMember>> GetMembersAsync(Guid orgId, CancellationToken cancellationToken = default)
        {
            var list = await ReadMembersAsync(cancellationToken).ConfigureAwait(false);
            return list.Where(m => m.OrganisationId == orgId).ToList();
        }

        public async Task<OrganisationMember> InviteMemberAsync(Guid orgId, string email, OrgRole role, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(email)) throw new ArgumentException("Email is required.", nameof(email));
            return await AddMemberAsync(orgId, email.Trim(), role, MemberStatus.Pending, null, cancellationToken).ConfigureAwait(false);
        }

        public async Task<OrganisationMember> AddMemberAsync(Guid orgId, string email, OrgRole role, MemberStatus status = MemberStatus.Active, string? displayName = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(email)) throw new ArgumentException("Email is required.", nameof(email));
            var list = await ReadMembersAsync(cancellationToken).ConfigureAwait(false);
            if (list.Any(m => m.OrganisationId == orgId && string.Equals(m.Email, email.Trim(), StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("A member with this email already exists in the organisation.");
            var member = new OrganisationMember
            {
                Id = Guid.NewGuid(),
                OrganisationId = orgId,
                Email = email.Trim(),
                DisplayName = displayName?.Trim(),
                Role = role,
                Status = status,
                JoinedAt = DateTime.UtcNow
            };
            list.Add(member);
            await WriteMembersAsync(list, cancellationToken).ConfigureAwait(false);
            return member;
        }

        public async Task UpdateRoleAsync(Guid memberId, OrgRole role, CancellationToken cancellationToken = default)
        {
            var list = await ReadMembersAsync(cancellationToken).ConfigureAwait(false);
            var member = list.FirstOrDefault(m => m.Id == memberId);
            if (member == null) throw new InvalidOperationException("Member not found.");
            member.Role = role;
            await WriteMembersAsync(list, cancellationToken).ConfigureAwait(false);
        }

        public async Task RemoveMemberAsync(Guid memberId, CancellationToken cancellationToken = default)
        {
            var list = await ReadMembersAsync(cancellationToken).ConfigureAwait(false);
            var member = list.FirstOrDefault(m => m.Id == memberId);
            if (member == null) throw new InvalidOperationException("Member not found.");
            var ownerCount = list.Count(m => m.OrganisationId == member.OrganisationId && m.Role == OrgRole.Owner && m.Status == MemberStatus.Active);
            if (member.Role == OrgRole.Owner && member.Status == MemberStatus.Active && ownerCount <= 1)
                throw new InvalidOperationException("Cannot remove the last owner.");
            list.Remove(member);
            await WriteMembersAsync(list, cancellationToken).ConfigureAwait(false);
        }

        private async Task<List<OrganisationMember>> ReadMembersAsync(CancellationToken cancellationToken)
        {
            var path = Path.Combine(_appPaths.AppData, MembersFileName);
            if (!File.Exists(path)) return new List<OrganisationMember>();
            try
            {
                var json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
                var list = JsonSerializer.Deserialize<List<OrganisationMember>>(json, JsonOptions);
                return list ?? new List<OrganisationMember>();
            }
            catch
            {
                return new List<OrganisationMember>();
            }
        }

        private async Task WriteMembersAsync(List<OrganisationMember> list, CancellationToken cancellationToken)
        {
            var path = Path.Combine(_appPaths.AppData, MembersFileName);
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(list, JsonOptions), cancellationToken).ConfigureAwait(false);
        }
    }
}
