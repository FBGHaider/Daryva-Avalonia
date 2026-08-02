using Microsoft.AspNetCore.Authorization;

namespace Daryva.Api.Security;

/// <summary>
/// Requires access to a specific organization (a Guid resource), checked against:
/// 1. The fast path -- resource org equals CurrentOrgId with a non-null CurrentRole (phase 12
///    already validated this org, real membership or Support Session elevation).
/// 2. Real membership of a DIFFERENT org than CurrentOrgId -- OrgsController intentionally lets
///    a caller manage any org they belong to by ID, not just the one currently selected (e.g. a
///    landlord with several portfolios renaming one without switching their active org first).
/// 3. A platform admin with an active Support Session on THIS specific org, independent of
///    CurrentOrgId -- so Support Mode works for org-management endpoints too, not just the ones
///    that operate on CurrentOrgId implicitly.
///
/// Used via a direct call, not a named policy:
///   await _authorizationService.AuthorizeAsync(User, orgId, new[] { new OrgResourceRequirement() });
/// </summary>
public class OrgResourceRequirement : IAuthorizationRequirement
{
}
