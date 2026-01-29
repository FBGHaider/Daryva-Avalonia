namespace Daryva.Services.Update;

/// <summary>
/// Information about an available update.
/// </summary>
public class UpdateInfo
{
    /// <summary>
    /// The version string of the available update (e.g. "1.2.0").
    /// </summary>
    public string Version { get; init; } = string.Empty;
}
