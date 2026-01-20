using System.Configuration;
using System.IO;
using System.Xml;

namespace LandLordBuddy.Services
{
    /// <summary>
    /// Implementation of IConfigurationService that reads from App.config and App.config.local.
    /// </summary>
    public class ConfigurationService : IConfigurationService
    {
        private const string DefaultConnectionStringName = "DefaultConnection";
        private static readonly string LocalConfigPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "App.config.local");
        private static Dictionary<string, string>? _localSettings;

        static ConfigurationService()
        {
            LoadLocalConfig();
        }

        /// <summary>
        /// Loads settings from App.config.local if it exists.
        /// </summary>
        private static void LoadLocalConfig()
        {
            _localSettings = new Dictionary<string, string>();
            
            if (File.Exists(LocalConfigPath))
            {
                try
                {
                    var doc = new XmlDocument();
                    doc.Load(LocalConfigPath);
                    
                    var nodes = doc.SelectNodes("//configuration/appSettings/add");
                    if (nodes != null)
                    {
                        foreach (XmlNode node in nodes)
                        {
                            var key = node.Attributes?["key"]?.Value;
                            var value = node.Attributes?["value"]?.Value;
                            if (key != null && value != null)
                            {
                                _localSettings[key] = value;
                            }
                        }
                    }
                }
                catch
                {
                    // If local config is invalid, ignore it
                    _localSettings.Clear();
                }
            }
        }

        /// <summary>
        /// Gets the database connection string from configuration.
        /// </summary>
        public string GetConnectionString()
        {
            var connectionString = ConfigurationManager.ConnectionStrings[DefaultConnectionStringName]?.ConnectionString;
            
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    $"Connection string '{DefaultConnectionStringName}' is not configured. " +
                    "Please add it to the App.config file.");
            }

            return connectionString;
        }

        /// <summary>
        /// Gets a configuration value by key from AppSettings.
        /// Checks App.config.local first, then falls back to App.config.
        /// </summary>
        public string? GetValue(string key)
        {
            // Check local config first (for sensitive data like SMTP credentials)
            if (_localSettings != null && _localSettings.TryGetValue(key, out var localValue))
            {
                return localValue;
            }
            
            // Fall back to App.config
            return ConfigurationManager.AppSettings[key];
        }

        /// <summary>
        /// Sets a configuration value in AppSettings.
        /// </summary>
        public void SetValue(string key, string value)
        {
            var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            config.AppSettings.Settings[key].Value = value;
            config.Save(ConfigurationSaveMode.Modified);
            ConfigurationManager.RefreshSection("appSettings");
        }
    }
}
