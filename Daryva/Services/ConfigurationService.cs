using System.Configuration;
using System.IO;
using System.Xml;

namespace Daryva.Services
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
        private static string? _localConnectionString;

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
            _localConnectionString = null;

            if (File.Exists(LocalConfigPath))
            {
                try
                {
                    var doc = new XmlDocument();
                    doc.Load(LocalConfigPath);

                    var appSettingsNodes = doc.SelectNodes("//configuration/appSettings/add");
                    if (appSettingsNodes != null)
                    {
                        foreach (XmlNode node in appSettingsNodes)
                        {
                            var key = node.Attributes?["key"]?.Value;
                            var value = node.Attributes?["value"]?.Value;
                            if (key != null && value != null)
                                _localSettings[key] = value;
                        }
                    }

                    var csNode = doc.SelectSingleNode($"//configuration/connectionStrings/add[@name='{DefaultConnectionStringName}']");
                    if (csNode != null)
                    {
                        var cs = csNode.Attributes?["connectionString"]?.Value;
                        if (!string.IsNullOrWhiteSpace(cs))
                            _localConnectionString = cs.Trim();
                    }
                }
                catch
                {
                    _localSettings.Clear();
                    _localConnectionString = null;
                }
            }
        }

        /// <summary>
        /// Gets the database connection string from configuration.
        /// Uses App.config.local connection string if present, otherwise App.config.
        /// </summary>
        public string GetConnectionString()
        {
            if (!string.IsNullOrWhiteSpace(_localConnectionString))
                return _localConnectionString;

            var connectionString = ConfigurationManager.ConnectionStrings[DefaultConnectionStringName]?.ConnectionString;

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    $"Connection string '{DefaultConnectionStringName}' is not configured. " +
                    "Please add it to the App.config file, or to App.config.local for a direct (non-Docker) database.");
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

        /// <summary>
        /// Sets a configuration value in App.config.local.
        /// Creates the file if it doesn't exist.
        /// </summary>
        public void SetLocalValue(string key, string value)
        {
            if (!File.Exists(LocalConfigPath))
            {
                // Create the file with basic structure
                var doc = new XmlDocument();
                var declaration = doc.CreateXmlDeclaration("1.0", "utf-8", null);
                doc.AppendChild(declaration);
                
                var configElement = doc.CreateElement("configuration");
                doc.AppendChild(configElement);
                
                var appSettingsElement = doc.CreateElement("appSettings");
                configElement.AppendChild(appSettingsElement);
                
                doc.Save(LocalConfigPath);
            }

            var doc2 = new XmlDocument();
            doc2.Load(LocalConfigPath);
            
            var appSettings = doc2.SelectSingleNode("//configuration/appSettings");
            if (appSettings == null)
            {
                var config = doc2.SelectSingleNode("//configuration");
                if (config == null)
                {
                    var configElement = doc2.CreateElement("configuration");
                    doc2.AppendChild(configElement);
                    config = configElement;
                }
                appSettings = doc2.CreateElement("appSettings");
                config.AppendChild(appSettings);
            }

            // Find existing add node with this key
            var existingNode = doc2.SelectSingleNode($"//configuration/appSettings/add[@key='{key}']");
            if (existingNode != null)
            {
                var valueAttr = existingNode.Attributes?["value"];
                if (valueAttr != null)
                {
                    valueAttr.Value = value;
                }
            }
            else
            {
                var addNode = doc2.CreateElement("add");
                addNode.SetAttribute("key", key);
                addNode.SetAttribute("value", value);
                appSettings.AppendChild(addNode);
            }

            doc2.Save(LocalConfigPath);
            
            // Reload the local settings cache
            LoadLocalConfig();
        }

        /// <summary>
        /// Reloads the local configuration file.
        /// </summary>
        public void ReloadLocalConfig()
        {
            LoadLocalConfig();
        }
    }
}
