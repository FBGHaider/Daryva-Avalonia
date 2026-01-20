namespace Daryva.Services.Business
{
    /// <summary>
    /// Service interface for managing application settings.
    /// </summary>
    public interface ISettingsService
    {
        /// <summary>
        /// Gets a setting value by key.
        /// </summary>
        Task<T?> GetSettingAsync<T>(string key, T? defaultValue = default) where T : struct;

        /// <summary>
        /// Gets a string setting value by key.
        /// </summary>
        Task<string?> GetSettingAsync(string key, string? defaultValue = null);

        /// <summary>
        /// Gets all settings for a category.
        /// </summary>
        Task<Dictionary<string, string>> GetCategorySettingsAsync(string category);

        /// <summary>
        /// Sets a setting value.
        /// </summary>
        Task SetSettingAsync<T>(string key, T value);

        /// <summary>
        /// Sets multiple settings.
        /// </summary>
        Task SetSettingsAsync(Dictionary<string, string> settings);

        /// <summary>
        /// Gets the database size in MB.
        /// </summary>
        Task<decimal> GetDatabaseSizeAsync();

        /// <summary>
        /// Gets the database connection status.
        /// </summary>
        Task<bool> IsDatabaseConnectedAsync();
    }
}
