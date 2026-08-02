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
    public const string TwoFactorChallengeIssued = "TwoFactorChallengeIssued";
    public const string TwoFactorVerified = "TwoFactorVerified";
    public const string TwoFactorFailed = "TwoFactorFailed";
    public const string TwoFactorRecoveryCodeUsed = "TwoFactorRecoveryCodeUsed";
    public const string TwoFactorDisabled = "TwoFactorDisabled";
    public const string TwoFactorRecoveryCodesRegenerated = "TwoFactorRecoveryCodesRegenerated";
    public const string SupportSessionStarted = "SupportSessionStarted";
    public const string SupportSessionEnded = "SupportSessionEnded";
    public const string HouseArchived = "HouseArchived";
    public const string HouseDeleted = "HouseDeleted";
    public const string TenantArchived = "TenantArchived";
    public const string TenantUnarchived = "TenantUnarchived";
    public const string TenantDeleted = "TenantDeleted";
    public const string TenancyEnded = "TenancyEnded";
    public const string TenancyReactivated = "TenancyReactivated";
    public const string TenancyDeleted = "TenancyDeleted";
    public const string TenanciesBulkDeleted = "TenanciesBulkDeleted";
    public const string DocumentDeleted = "DocumentDeleted";
    public const string ExpenseDeleted = "ExpenseDeleted";
    public const string PaymentRecorded = "PaymentRecorded";
    public const string DepositReturnRecorded = "DepositReturnRecorded";
    public const string PaymentDeleted = "PaymentDeleted";
    public const string PaymentsBulkDeleted = "PaymentsBulkDeleted";
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
