using System.Diagnostics;
using System.Reflection;
using Daryva.Services;
using Velopack;
using Velopack.Sources;

namespace Daryva.Services.Update;

/// <summary>
/// Velopack-based implementation of IUpdateService.
/// Uses SimpleWebSource: expects feed at baseUrl/RELEASES (or releases.{channel}.json).
/// </summary>
public class VelopackUpdateService : IUpdateService
{
    private readonly IConfigurationService _configService;
    private UpdateManager? _updateManager;
    private bool _managerInitialized;

    private const string UpdateFeedUrlKey = "UpdateFeedUrl";
    private const string UpdateFeedTokenKey = "UpdateFeedToken";

    /// <summary>
    /// Default update feed when not configured. GitHub Releases for Daryva-Updates repo.
    /// </summary>
    private const string DefaultUpdateFeedUrl = "https://github.com/FBGHaider/Daryva-Updates";

    public VelopackUpdateService(IConfigurationService configService)
    {
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
    }

    private UpdateManager? GetUpdateManager()
    {
        if (_managerInitialized)
            return _updateManager;

        _managerInitialized = true;

        var feedUrl = _configService.GetValue(UpdateFeedUrlKey) ?? DefaultUpdateFeedUrl;
        if (string.IsNullOrWhiteSpace(feedUrl))
        {
            Debug.WriteLine("[VelopackUpdateService] UpdateFeedUrl not configured. Skipping update manager.");
            return _updateManager;
        }

        feedUrl = feedUrl.TrimEnd('/');
        var accessToken = _configService.GetValue(UpdateFeedTokenKey)?.Trim();
        if (string.IsNullOrEmpty(accessToken))
            accessToken = null;

        try
        {
            IUpdateSource source = feedUrl.Contains("github.com", StringComparison.OrdinalIgnoreCase)
                ? new GithubSource(feedUrl, accessToken ?? string.Empty, prerelease: false)
                : new SimpleWebSource(feedUrl);
            _updateManager = new UpdateManager(source);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[VelopackUpdateService] Failed to create UpdateManager: {ex.Message}");
        }

        return _updateManager;
    }

    public bool IsInstalled
    {
        get
        {
            try
            {
                var mgr = GetUpdateManager();
                return mgr?.IsInstalled ?? false;
            }
            catch
            {
                return false;
            }
        }
    }

    public string CurrentVersion
    {
        get
        {
            try
            {
                var mgr = GetUpdateManager();
                if (mgr != null && mgr.IsInstalled && mgr.CurrentVersion != null)
                    return mgr.CurrentVersion.ToNormalizedString();

                var version = Assembly.GetExecutingAssembly().GetName().Version;
                return version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "0.0.0";
            }
            catch
            {
                return "0.0.0";
            }
        }
    }

    public async Task<UpdateInfo?> CheckAsync(CancellationToken ct = default)
    {
        var mgr = GetUpdateManager();
        if (mgr == null || !mgr.IsInstalled)
            return null;

        try
        {
            var vpUpdateInfo = await mgr.CheckForUpdatesAsync().WaitAsync(ct);
            if (vpUpdateInfo == null)
                return null;

            var version = vpUpdateInfo.TargetFullRelease?.Version?.ToNormalizedString() ?? "0.0.0";
            return new UpdateInfo { Version = version };
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[VelopackUpdateService] Check failed: {ex.Message}");
            throw; // Let caller show "Update check failed" instead of "Up to date"
        }
    }

    public async Task DownloadAndApplyAsync(IProgress<int>? progressPercent = null, CancellationToken ct = default)
    {
        var mgr = GetUpdateManager();
        if (mgr == null || !mgr.IsInstalled)
            throw new InvalidOperationException("Updates are not available (app not installed via Velopack).");

        var vpUpdateInfo = await mgr.CheckForUpdatesAsync().WaitAsync(ct);
        if (vpUpdateInfo == null)
            throw new InvalidOperationException("No update available.");

        void ProgressCallback(int percent)
        {
            progressPercent?.Report(percent);
        }

        await mgr.DownloadUpdatesAsync(vpUpdateInfo, ProgressCallback, ct).WaitAsync(ct);
        mgr.ApplyUpdatesAndRestart(vpUpdateInfo);
    }
}
