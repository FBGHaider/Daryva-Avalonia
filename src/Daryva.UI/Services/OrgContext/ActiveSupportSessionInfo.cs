namespace Daryva.Services.OrgContext;

/// <summary>
/// Details of the live Support Session behind IOrgContext.EnterSupportOrgAsync, for the
/// always-visible "you're in a support session" banner. Null outside an active elevation.
/// </summary>
public sealed class ActiveSupportSessionInfo
{
    public Guid SessionId { get; init; }
    public Guid OrganizationId { get; init; }
    public string OrganizationName { get; init; } = string.Empty;
    public DateTime ExpiresAtUtc { get; init; }
}
