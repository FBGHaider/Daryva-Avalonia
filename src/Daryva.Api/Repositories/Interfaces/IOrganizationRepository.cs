using Daryva.Api.Domain;

namespace Daryva.Api.Repositories.Interfaces;

/// <summary>
/// Repository for the Organization aggregate: the org itself plus its join mechanisms
/// (invites, join codes). Membership lives separately in IOrganizationMemberRepository.
/// </summary>
public interface IOrganizationRepository
{
    Task<Organization?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    void Add(Organization organization);
    void Remove(Organization organization);

    /// <summary>Batch lookup, e.g. resolving orgs for a set of matched OrganizationMember rows.</summary>
    Task<List<Organization>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);

    Task<OrganizationInvite?> GetInviteByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);
    void AddInvite(OrganizationInvite invite);

    Task<List<OrganizationJoinCode>> GetActiveJoinCodesAsync(Guid organizationId, CancellationToken cancellationToken = default);
    Task<OrganizationJoinCode?> GetJoinCodeByCodeHashAsync(string codeHash, CancellationToken cancellationToken = default);
    void AddJoinCode(OrganizationJoinCode joinCode);
}
