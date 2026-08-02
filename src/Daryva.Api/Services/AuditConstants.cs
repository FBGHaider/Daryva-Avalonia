namespace Daryva.Api.Services;

/// <summary>
/// AuditLog.EventType values. Grows across phases (role changes, archives/deletes,
/// payment changes, support-session activity get added in later phases).
/// </summary>
public static class AuditEventTypes
{
    public const string AuthRegister = "AuthRegister";
    public const string AuthLogin = "AuthLogin";
    public const string AuthLoginFailed = "AuthLoginFailed";
    public const string AuthLoginBlocked = "AuthLoginBlocked";
    public const string AuthPasswordResetRequested = "AuthPasswordResetRequested";
    public const string AuthPasswordReset = "AuthPasswordReset";
    public const string PlatformAdminGranted = "PlatformAdminGranted";
    public const string TwoFactorEnabled = "TwoFactorEnabled";
}

/// <summary>
/// AuditLog.ActorRole values. "User" covers self-service account actions (register,
/// login, password reset) that happen before any org role applies. Admin/Landlord get
/// used once org-scoped events are instrumented (phase 26+).
/// </summary>
public static class AuditActorRoles
{
    public const string User = "User";

    /// <summary>Automated/system-initiated action with no human actor. Paired with ActorUserId = Guid.Empty.</summary>
    public const string System = "System";
}
