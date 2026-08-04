namespace Daryva.Api.Dtos;

public class StartSupportSessionRequest
{
    public Guid OrganizationId { get; set; }

    /// <summary>Required -- no silent access. E.g. "Ticket #482: landlord's rent ledger is out of sync".</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>Minutes until the session auto-expires. Clamped to [5, 240]; defaults to 60 if omitted.</summary>
    public int? DurationMinutes { get; set; }
}

public class SupportSessionResponse
{
    public Guid Id { get; set; }
    public Guid AdminUserId { get; set; }
    public Guid OrganizationId { get; set; }
    public string OrganizationName { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public string? EndedReason { get; set; }
    public bool IsActive { get; set; }
}
