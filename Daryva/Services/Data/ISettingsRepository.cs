namespace Daryva.Services.Data
{
    /// <summary>
    /// Repository interface for application settings data access.
    /// </summary>
    public interface ISettingsRepository
    {
        /// <summary>
        /// Gets a setting value by key.
        /// </summary>
        Task<string?> GetSettingValueAsync(string key);

        /// <summary>
        /// Gets all settings for a specific category.
        /// </summary>
        Task<Dictionary<string, string>> GetSettingsByCategoryAsync(string category);

        /// <summary>
        /// Gets all settings.
        /// </summary>
        Task<Dictionary<string, string>> GetAllSettingsAsync();

        /// <summary>
        /// Sets a setting value.
        /// </summary>
        Task SetSettingValueAsync(string key, string value, string settingType = "String");

        /// <summary>
        /// Sets multiple setting values.
        /// </summary>
        Task SetSettingsAsync(Dictionary<string, string> settings);

        /// <summary>
        /// Deletes a setting.
        /// </summary>
        Task DeleteSettingAsync(string key);
    }
}
