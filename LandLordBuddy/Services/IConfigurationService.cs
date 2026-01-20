namespace LandLordBuddy.Services
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
    }
}
