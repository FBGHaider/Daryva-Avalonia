namespace Daryva.Api.Domain;

/// <summary>
/// A short-lived, single-use code a real org member generates so a platform admin can look up
/// their exact organization without searching by email -- e.g. read out over a phone call.
/// Resolving a code identifies the org only; it does not itself grant access or start a Support
/// Session (see SupportAccessCodeService.ResolveAsync / SupportSessionService.StartAsync). Not an
/// IOrgScopedEntity for the same reason as SupportSession: the resolving admin has no org context
/// of their own, so a CurrentOrgId query filter would break the lookup this entity exists for.
/// </summary>
public class SupportAccessCode
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public string Code { get; set; } = string.Empty;

    /// <summary>The org member (Landlord) who generated this code.</summary>
    public string CreatedByUserId { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }

    /// <summary>Set the moment an admin successfully resolves this code -- makes it single-use.</summary>
    public Guid? ResolvedByAdminId { get; set; }
    public DateTime? ResolvedAt { get; set; }
}
