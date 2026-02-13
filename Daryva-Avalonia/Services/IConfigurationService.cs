namespace Daryva.Services
{
    /// <summary>
    /// Service for managing application configuration settings.
    /// </summary>
    public interface IConfigurationService
    {
        /// <summary>
        /// Gets the database connection string.
        /// </summary>
        string GetConnectionString();

        /// <summary>
        /// Returns the current database file path (for display and export).
        /// </summary>
        string GetCurrentDatabasePath();

        /// <summary>
        /// Gets a configuration value by key.
        /// </summary>
        /// <param name="key">The configuration key.</param>
        /// <returns>The configuration value, or null if not found.</returns>
        string? GetValue(string key);

        /// <summary>
        /// Sets a configuration value.
        /// </summary>
        /// <param name="key">The configuration key.</param>
        /// <param name="value">The configuration value.</param>
        void SetValue(string key, string value);

        /// <summary>
        /// Sets a configuration value in App.config.local.
        /// </summary>
        /// <param name="key">The configuration key.</param>
        /// <param name="value">The configuration value.</param>
        void SetLocalValue(string key, string value);

        /// <summary>
        /// Reloads the local configuration file.
        /// </summary>
        void ReloadLocalConfig();
    }
}
