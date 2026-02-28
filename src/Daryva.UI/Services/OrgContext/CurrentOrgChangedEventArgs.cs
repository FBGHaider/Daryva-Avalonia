namespace Daryva.Services.OrgContext;

/// <summary>
/// Raised when the current organisation changes (e.g. via SetCurrentOrgAsync or org switch).
/// </summary>
public sealed class CurrentOrgChangedEventArgs : EventArgs
{
    public Guid? NewOrgId { get; init; }
}
