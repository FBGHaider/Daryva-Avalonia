namespace Daryva.Services.Settings
{
    /// <summary>
    /// Service for storing and retrieving application settings.
    /// </summary>
    public interface ISettingsStore
    {
        /// <summary>
        /// Gets a setting value by key, or returns the default if not found.
        /// </summary>
        string? GetSetting(string key, string? defaultValue = null);

        /// <summary>
        /// Sets a setting value by key.
        /// </summary>
        void SetSetting(string key, string value);

        /// <summary>
        /// Removes a setting by key.
        /// </summary>
        void RemoveSetting(string key);

        /// <summary>
        /// Clears all settings.
        /// </summary>
        void Clear();
    }
}
