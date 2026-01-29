namespace Daryva.Services.Platform
{
    /// <summary>
    /// Provides cross-platform application paths.
    /// </summary>
    public interface IAppPaths
    {
        /// <summary>
        /// Gets the application data directory (e.g., AppData\Roaming\Daryva on Windows, ~/Library/Application Support/Daryva on macOS).
        /// </summary>
        string AppData { get; }

        /// <summary>
        /// Gets the logs directory (typically under AppData\Logs).
        /// </summary>
        string Logs { get; }

        /// <summary>
        /// Gets the default exports directory (typically MyDocuments\Daryva Exports).
        /// </summary>
        string Exports { get; }

        /// <summary>
        /// Gets the database directory (typically under AppData).
        /// </summary>
        string Database { get; }
    }
}
