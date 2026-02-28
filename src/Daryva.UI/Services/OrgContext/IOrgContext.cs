namespace Daryva.Services.OrgContext;

/// <summary>
/// Current user's organisation context from GET /api/me.
/// Exposes org list, current org, onboarding flags; persists CurrentOrgId.
/// </summary>
public interface IOrgContext
{
    Guid? CurrentOrgId { get; }
    IReadOnlyList<OrgSummary> Orgs { get; }
    OrgSummary? CurrentOrg { get; }
    bool RequiresOnboarding { get; }
    bool RequiresProfile { get; }

    Task RefreshAsync(CancellationToken cancellationToken = default);
    Task SetCurrentOrgAsync(Guid orgId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Raised when the current org changes (e.g. after SetCurrentOrgAsync). Subscribe to trigger app-wide data refresh.
    /// </summary>
    event EventHandler<CurrentOrgChangedEventArgs>? CurrentOrgChanged;
}
