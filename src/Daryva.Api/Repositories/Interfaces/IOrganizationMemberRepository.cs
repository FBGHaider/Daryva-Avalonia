using Daryva.Api.Domain;

namespace Daryva.Api.Repositories.Interfaces;

public interface IOrganizationMemberRepository
{
    Task<OrganizationMember?> GetMembershipAsync(string userId, Guid organizationId, CancellationToken cancellationToken = default);

    Task<List<OrganizationMember>> GetByOrganizationIdAsync(Guid organizationId, CancellationToken cancellationToken = default);
}
