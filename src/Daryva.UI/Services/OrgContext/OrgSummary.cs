namespace Daryva.Services.OrgContext;

/// <summary>
/// Summary of an organisation for org context / switcher.
/// </summary>
public sealed class OrgSummary
{
    /// <summary>Role marker for an org entered via IOrgContext.EnterSupportOrgAsync -- not a real membership.</summary>
    public const string SupportSessionRole = "Support";

    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}
