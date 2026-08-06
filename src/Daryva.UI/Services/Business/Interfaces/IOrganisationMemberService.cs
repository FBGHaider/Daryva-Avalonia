using Daryva.MVVM.Models;

namespace Daryva.Services.Business
{
    /// <summary>
    /// Service for organisation members/invites (local MVP; can be replaced by SaaS API later).
    /// Every member is a Landlord (the only real org-scoped role) -- there is no role parameter
    /// to choose, since the backend has no tier below/above it to assign.
    /// </summary>
    public interface IOrganisationMemberService
    {
        Task<IReadOnlyList<OrganisationMember>> GetMembersAsync(Guid orgId, CancellationToken cancellationToken = default);
        Task<OrganisationMember> InviteMemberAsync(Guid orgId, string email, CancellationToken cancellationToken = default);
        /// <summary>Add a member directly (e.g. Active owner for seeding).</summary>
        Task<OrganisationMember> AddMemberAsync(Guid orgId, string email, MemberStatus status = MemberStatus.Active, string? displayName = null, bool isPrimaryOwner = false, CancellationToken cancellationToken = default);
        Task RemoveMemberAsync(Guid memberId, CancellationToken cancellationToken = default);
    }
}
