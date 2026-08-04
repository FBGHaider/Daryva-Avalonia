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

    /// <summary>AppUser.IsPlatformAdmin for the signed-in user, from GET /api/me. False until RefreshAsync has run at least once.</summary>
    bool IsPlatformAdmin { get; }

    /// <summary>Non-null while acting inside an org entered via EnterSupportOrgAsync -- drives the
    /// always-visible "live support session" banner. Cleared by RefreshAsync, ClearForSignOut, and
    /// ExitSupportOrgAsync.</summary>
    ActiveSupportSessionInfo? ActiveSupportSession { get; }

    Task RefreshAsync(CancellationToken cancellationToken = default);
    Task SetCurrentOrgAsync(Guid orgId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the current org by id and name (e.g. after restore from recovery code). Adds the org to the list if not present.
    /// </summary>
    Task SetCurrentOrgFromRecoveryAsync(Guid orgId, string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the current org to one the caller is NOT a real member of, for a platform admin acting
    /// under an active Support Session (server-side, TenantContextMiddleware/OrgResourceAuthorizationHandler
    /// independently verify the session is real and active on every request -- this is purely client
    /// display/routing state, not a source of authority). Unlike SetCurrentOrgAsync, this does not
    /// require the org to already be in Orgs, and does not persist across restarts: the next
    /// RefreshAsync (e.g. app relaunch) drops back to the admin's real memberships, so a stale
    /// support-org selection can never silently outlive its actual server-side session. No-ops if
    /// IsPlatformAdmin is false.
    /// </summary>
    Task EnterSupportOrgAsync(Guid orgId, string orgName, Guid sessionId, DateTime expiresAtUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reverses EnterSupportOrgAsync: drops the synthetic Support org, clears ActiveSupportSession,
    /// and re-syncs from /api/me (RefreshAsync) so CurrentOrgId falls back to a real membership (or
    /// null, for an admin with none). Raises CurrentOrgChanged so already-open org-scoped screens
    /// (Houses, Dashboard, etc., which all subscribe to it) reload instead of continuing to show
    /// stale data or erroring against an org they no longer have access to. No-ops if
    /// ActiveSupportSession is already null. Call this whenever the session behind the current
    /// EnterSupportOrgAsync ends -- manually, or because it expired.
    /// </summary>
    Task ExitSupportOrgAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Raised when the current org changes (e.g. after SetCurrentOrgAsync). Subscribe to trigger app-wide data refresh.
    /// </summary>
    event EventHandler<CurrentOrgChangedEventArgs>? CurrentOrgChanged;

    /// <summary>
    /// Raised when current org details (e.g. name) change. Subscribe to refresh the org label in the UI.
    /// </summary>
    event EventHandler? CurrentOrgDetailsChanged;

    /// <summary>Notifies that current org details (e.g. name) changed so subscribers can refresh.</summary>
    void NotifyCurrentOrgDetailsChanged();

    /// <summary>Clears only the current org selection (keeps Orgs list). Use when switching or when no org should be sent (e.g. X-Org-Id absent).</summary>
    void ClearCurrentOrgSelection();

    /// <summary>Clears in-memory org state when the user signs out so the next sign-in does not show stale data.</summary>
    void ClearForSignOut();
}
