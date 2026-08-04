using Daryva.Api.Domain;

namespace Daryva.Api.Repositories.Interfaces;

public interface IOrganizationMemberRepository
{
    Task<OrganizationMember?> GetMembershipAsync(string userId, Guid organizationId, CancellationToken cancellationToken = default);

    Task<List<OrganizationMember>> GetByOrganizationIdAsync(Guid organizationId, CancellationToken cancellationToken = default);

    /// <summary>Batch lookup for enriching an org list (e.g. owner email, member count) without one query per org.</summary>
    Task<List<OrganizationMember>> GetByOrganizationIdsAsync(IEnumerable<Guid> organizationIds, CancellationToken cancellationToken = default);

    /// <summary>Includes the Organization navigation -- callers listing a user's orgs need the org name/details.</summary>
    Task<List<OrganizationMember>> GetByUserIdWithOrganizationAsync(string userId, CancellationToken cancellationToken = default);

    Task<bool> AnyForUserAsync(string userId, CancellationToken cancellationToken = default);

    Task<OrganizationMember?> GetByEmailAsync(Guid organizationId, string email, CancellationToken cancellationToken = default);

    void Add(OrganizationMember member);
}
