using System.IO;
using System.Linq;
using System.Text;

namespace Daryva.Services;

/// <summary>
/// Lightweight, always-on session logger. Every process run writes one timestamped file to
/// IAppPaths.Logs, so after reproducing a bug the user can open Settings -> copy the whole
/// session's log (or the file itself) and hand it over instead of a screenshot + guesswork.
/// Deliberately a static class, not a DI service: DialogService and other low-level pieces that
/// have no natural place to inject a logger (or run before the container finishes building) still
/// need to log, and a single process-wide log file makes more sense than one per DI scope anyway.
/// </summary>
public static class AppLogger
{
    private static readonly object Gate = new();
    private static string? _logFilePath;

    public static string? CurrentLogFilePath => _logFilePath;

    public static void Initialize(string logsDirectory)
    {
        lock (Gate)
        {
            try
            {
                Directory.CreateDirectory(logsDirectory);
                _logFilePath = Path.Combine(logsDirectory, $"session_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.log");

                // Keep the folder from growing forever -- only the most recent 20 sessions are kept.
                var stale = new DirectoryInfo(logsDirectory)
                    .GetFiles("session_*.log")
                    .OrderByDescending(f => f.CreationTimeUtc)
                    .Skip(20);
                foreach (var file in stale)
                {
                    try { file.Delete(); } catch { /* best effort */ }
                }
            }
            catch
            {
                _logFilePath = null;
            }
        }

        WriteLine($"=== Session started {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} ===");
    }

    /// <summary>General event: navigation, org switch, dialog shown, etc.</summary>
    public static void Log(string category, string message) => WriteLine($"[{category}] {message}");

    /// <summary>Same as Log but tagged ERROR so it stands out when scanning/grepping the file.</summary>
    public static void LogError(string category, string message) => WriteLine($"[{category}] [ERROR] {message}");

    private static void WriteLine(string text)
    {
        var line = $"[{DateTime.Now:HH:mm:ss.fff}] {text}";
        System.Diagnostics.Debug.WriteLine(line);

        if (_logFilePath == null)
            return;

        lock (Gate)
        {
            try { File.AppendAllText(_logFilePath, line + Environment.NewLine); }
            catch { /* logging must never crash the app it's observing */ }
        }
    }

    /// <summary>Reads the current session's full log text (for "copy logs" in Settings).</summary>
    public static string ReadCurrentSessionLog()
    {
        if (_logFilePath == null)
            return string.Empty;

        lock (Gate)
        {
            try
            {
                using var stream = new FileStream(_logFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(stream, Encoding.UTF8);
                return reader.ReadToEnd();
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
