using Daryva.MVVM.Models;

namespace Daryva.Services.Business
{
    /// <summary>
    /// Service for organisation members/invites (local MVP; can be replaced by SaaS API later).
    /// </summary>
    public interface IOrganisationMemberService
    {
        Task<IReadOnlyList<OrganisationMember>> GetMembersAsync(Guid orgId, CancellationToken cancellationToken = default);
        Task<OrganisationMember> InviteMemberAsync(Guid orgId, string email, OrgRole role, CancellationToken cancellationToken = default);
        /// <summary>Add a member directly (e.g. Active owner for seeding).</summary>
        Task<OrganisationMember> AddMemberAsync(Guid orgId, string email, OrgRole role, MemberStatus status = MemberStatus.Active, string? displayName = null, CancellationToken cancellationToken = default);
        Task UpdateRoleAsync(Guid memberId, OrgRole role, CancellationToken cancellationToken = default);
        Task RemoveMemberAsync(Guid memberId, CancellationToken cancellationToken = default);
    }
}
