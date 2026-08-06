namespace Daryva.Services.Update;

/// <summary>
/// Service for checking and applying application updates via Velopack.
/// UI layer only - does not run in Core/Application.
/// </summary>
public interface IUpdateService
{
    /// <summary>
    /// Checks for available updates. Returns null if none available or if check fails.
    /// </summary>
    Task<UpdateInfo?> CheckAsync(CancellationToken ct = default);

    /// <summary>
    /// Downloads the update and applies it, then restarts the application.
    /// Call only when an update is available (after CheckAsync returned non-null).
    /// </summary>
    /// <param name="progressPercent">Progress callback (0-100).</param>
    Task DownloadAndApplyAsync(IProgress<int>? progressPercent = null, CancellationToken ct = default);

    /// <summary>
    /// True if the app is running from a Velopack install (can check/download updates).
    /// False when running from source or portable build.
    /// </summary>
    bool IsInstalled { get; }

    /// <summary>
    /// Current installed version, or assembly version when not installed.
    /// </summary>
    string CurrentVersion { get; }
}
