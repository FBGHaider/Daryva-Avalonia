namespace Daryva.Services.OrgContext;

/// <summary>
/// Summary of an organisation for org context / switcher.
/// </summary>
public sealed class OrgSummary
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}
