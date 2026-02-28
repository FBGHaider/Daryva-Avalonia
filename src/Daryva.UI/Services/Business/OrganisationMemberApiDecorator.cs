using System.Net.Http;
using Daryva.MVVM.Models;
using Daryva.Services.Api;

namespace Daryva.Services.Business;

/// <summary>
/// Decorator that uses the organization API for GetMembersAsync when available,
/// so the Organisation page shows API-backed members for the current org instead of local-only data.
/// Other operations (invite, add, update role, remove) delegate to the local service.
/// </summary>
public class OrganisationMemberApiDecorator : IOrganisationMemberService
{
    private readonly IOrganizationApiService _organizationApiService;
    private readonly OrganisationMemberService _localService;

    public OrganisationMemberApiDecorator(
        IOrganizationApiService organizationApiService,
        OrganisationMemberService localService)
    {
        _organizationApiService = organizationApiService ?? throw new ArgumentNullException(nameof(organizationApiService));
        _localService = localService ?? throw new ArgumentNullException(nameof(localService));
    }

    public async Task<IReadOnlyList<OrganisationMember>> GetMembersAsync(Guid orgId, CancellationToken cancellationToken = default)
    {
        try
        {
            var dtos = await _organizationApiService.GetOrganizationMembersAsync(orgId, cancellationToken).ConfigureAwait(false);
            return dtos.Select(d => new OrganisationMember
            {
                Id = d.Id,
                OrganisationId = orgId,
                Email = d.Email ?? string.Empty,
                DisplayName = null,
                Role = ParseRole(d.Role),
                Status = MemberStatus.Active,
                JoinedAt = d.JoinedAt
            }).ToList();
        }
        catch (HttpRequestException)
        {
            return await _localService.GetMembersAsync(orgId, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
            return await _localService.GetMembersAsync(orgId, cancellationToken).ConfigureAwait(false);
        }
    }

    public Task<OrganisationMember> InviteMemberAsync(Guid orgId, string email, OrgRole role, CancellationToken cancellationToken = default)
        => _localService.InviteMemberAsync(orgId, email, role, cancellationToken);

    public Task<OrganisationMember> AddMemberAsync(Guid orgId, string email, OrgRole role, MemberStatus status = MemberStatus.Active, string? displayName = null, CancellationToken cancellationToken = default)
        => _localService.AddMemberAsync(orgId, email, role, status, displayName, cancellationToken);

    public Task UpdateRoleAsync(Guid memberId, OrgRole role, CancellationToken cancellationToken = default)
        => _localService.UpdateRoleAsync(memberId, role, cancellationToken);

    public Task RemoveMemberAsync(Guid memberId, CancellationToken cancellationToken = default)
        => _localService.RemoveMemberAsync(memberId, cancellationToken);

    private static OrgRole ParseRole(string role)
    {
        if (string.IsNullOrWhiteSpace(role)) return OrgRole.Member;
        if (string.Equals(role, "Owner", StringComparison.OrdinalIgnoreCase)) return OrgRole.Owner;
        if (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase)) return OrgRole.Admin;
        if (string.Equals(role, "ReadOnly", StringComparison.OrdinalIgnoreCase)) return OrgRole.ReadOnly;
        return OrgRole.Member;
    }
}
