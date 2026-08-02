namespace Daryva.Api.Domain;

/// <summary>
/// Immutable record of a security/business-relevant event: auth events, role changes,
/// archives/deletes, payment record changes, support-session activity.
/// Not org-scoped (OrganizationId is nullable -- platform-level events have no org),
/// so it is intentionally NOT an IOrgScopedEntity: visibility rules (Admin sees all,
/// Landlord sees only their own org) are enforced by the query layer, not a global filter.
/// </summary>
public class AuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? OrganizationId { get; set; }
    public Guid ActorUserId { get; set; }

    /// <summary>Actor's role at the time of the action (Admin/Landlord) -- a snapshot, not a live lookup.</summary>
    public string ActorRole { get; set; } = string.Empty;

    /// <summary>e.g. AuthLogin, AuthLoginFailed, RoleChanged, PropertyArchived, PaymentRecorded, SupportSessionStarted.</summary>
    public string EventType { get; set; } = string.Empty;

    public string? TargetType { get; set; }
    public string? TargetId { get; set; }

    /// <summary>Free-form JSON with event-specific details (e.g. { "oldRole": "...", "newRole": "..." }).</summary>
    public string? Metadata { get; set; }

    /// <summary>Set when this action was performed under an active admin Support Session.</summary>
    public Guid? SupportSessionId { get; set; }

    public string? IpAddress { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
