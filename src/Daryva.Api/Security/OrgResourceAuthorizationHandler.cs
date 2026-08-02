using Daryva.Api.Repositories.Interfaces;
using Daryva.Api.Security.Interfaces;
using Microsoft.AspNetCore.Authorization;

namespace Daryva.Api.Security;

/// <summary>
/// See OrgResourceRequirement for what this checks and why.
/// </summary>
public class OrgResourceAuthorizationHandler : AuthorizationHandler<OrgResourceRequirement, Guid>
{
    private readonly ITenantContext _tenantContext;
    private readonly ISupportSessionRepository _supportSessionRepository;

    public OrgResourceAuthorizationHandler(ITenantContext tenantContext, ISupportSessionRepository supportSessionRepository)
    {
        _tenantContext = tenantContext;
        _supportSessionRepository = supportSessionRepository;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        OrgResourceRequirement requirement,
        Guid resourceOrgId)
    {
        if (_tenantContext.CurrentOrgId == resourceOrgId && _tenantContext.CurrentRole != null)
        {
            context.Succeed(requirement);
            return;
        }

        if (await _tenantContext.IsMemberOfOrgAsync(resourceOrgId))
        {
            context.Succeed(requirement);
            return;
        }

        if (_tenantContext.IsPlatformAdmin && Guid.TryParse(_tenantContext.UserId, out var adminUserId))
        {
            var activeSession = await _supportSessionRepository.GetActiveSessionAsync(adminUserId, resourceOrgId, CancellationToken.None);
            if (activeSession != null)
                context.Succeed(requirement);
        }
    }
}
