namespace Daryva.Api.Domain;

/// <summary>
/// A time-boxed, logged elevation of a platform Admin's access to one Landlord's org data.
/// Not an IOrgScopedEntity -- same reasoning as AuditLog/AppUser: an admin's default
/// OrganizationId context is null (no org membership), so a query filtered by
/// ITenantContext.CurrentOrgId would break exactly the "does an active session exist for
/// this admin+org" lookup this entity exists to answer.
///
/// While a row exists with EndedAt == null and ExpiresAt in the future, the admin's effective
/// permissions for OrganizationId elevate to Landlord-equivalent (phase 12). Every action taken
/// during that window is tagged with this session's Id on the corresponding AuditLog row, so
/// the landlord sees it in their own org's audit trail (see design discussion).
/// </summary>
public class SupportSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AdminUserId { get; set; }
    public Guid OrganizationId { get; set; }

    /// <summary>Required -- no silent access. E.g. "Ticket #482: landlord's rent ledger is out of sync".</summary>
    public string Reason { get; set; } = string.Empty;

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public DateTime? EndedAt { get; set; }

    /// <summary>SupportSessionEndedReasons.Expired or ManuallyEnded. Null while active.</summary>
    public string? EndedReason { get; set; }
}

public static class SupportSessionEndedReasons
{
    public const string Expired = "Expired";
    public const string ManuallyEnded = "ManuallyEnded";
}
