using System.IO;
using System.Text.Json;
using Daryva.Services.Platform;

namespace Daryva.Services
{
    /// <summary>
    /// Cross-platform implementation of IConfigurationService that reads from JSON config files.
    /// Replaces Windows-only ConfigurationManager.
    /// In API-first mode, this service still manages local app settings and local storage connection details.
    /// </summary>
    public class ConfigurationService : IConfigurationService
    {
        private const string DefaultConnectionStringName = "DefaultConnection";
        private static readonly string ConfigPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Daryva",
            "app.config.json");
        private static readonly string LocalConfigPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Daryva",
            "app.config.local.json");
        
        private static Dictionary<string, string>? _settings;
        private static Dictionary<string, string>? _localSettings;
        private static string? _localConnectionString;

        static ConfigurationService()
        {
            LoadConfig();
        }

        /// <summary>
        /// Loads settings from app.config.json and app.config.local.json if they exist.
        /// </summary>
        private static void LoadConfig()
        {
            _settings = new Dictionary<string, string>();
            _localSettings = new Dictionary<string, string>();
            _localConnectionString = null;

            // Load app.config.json (if exists)
            if (File.Exists(ConfigPath))
            {
                try
                {
                    var json = File.ReadAllText(ConfigPath);
                    var config = JsonSerializer.Deserialize<ConfigFile>(json);
                    if (config?.AppSettings != null)
                    {
                        _settings = config.AppSettings;
                    }
                    if (config?.ConnectionStrings != null && 
                        config.ConnectionStrings.TryGetValue(DefaultConnectionStringName, out var cs))
                    {
                        // Store default connection string if not overridden by local
                    }
                }
                catch
                {
                    _settings = new Dictionary<string, string>();
                }
            }

            // Load app.config.local.json (if exists) - takes precedence
            if (File.Exists(LocalConfigPath))
            {
                try
                {
                    var json = File.ReadAllText(LocalConfigPath);
                    var config = JsonSerializer.Deserialize<ConfigFile>(json);
                    if (config?.AppSettings != null)
                    {
                        _localSettings = config.AppSettings;
                    }
                    if (config?.ConnectionStrings != null && 
                        config.ConnectionStrings.TryGetValue(DefaultConnectionStringName, out var cs))
                    {
                        _localConnectionString = cs;
                    }
                }
                catch
                {
                    _localSettings = new Dictionary<string, string>();
                    _localConnectionString = null;
                }
            }
        }

        /// <summary>
        /// Gets the local storage connection string from configuration for UI-side repositories/settings.
        /// Priority: 1) installer database path file (databasepath.txt), 2) app.config.local.json,
        /// 3) app.config.json, 4) default AppData.
        /// This does not control API endpoint selection.
        /// </summary>

        public string GetConnectionString()
        {
            // Installer / custom database location: file written by installer next to the app.
            var appDir = AppContext.BaseDirectory;
            var databasePathFile = Path.Combine(appDir, "databasepath.txt");
            if (File.Exists(databasePathFile))
            {
                try
                {
                    var customFolder = File.ReadAllText(databasePathFile).Trim();
                    if (!string.IsNullOrWhiteSpace(customFolder))
                    {
                        if (!Directory.Exists(customFolder))
                            Directory.CreateDirectory(customFolder);
                        var dbPath = Path.Combine(customFolder, "DaryvaDB.db");
                        return $"Data Source={dbPath};";
                    }
                }
                catch
                {
                    // Fall through
                }
            }

            if (!string.IsNullOrWhiteSpace(_localConnectionString))
                return _localConnectionString;

            if (_settings != null && _settings.TryGetValue($"ConnectionStrings:{DefaultConnectionStringName}", out var connectionString))
                return connectionString;

            // Fallback to default AppData location
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var defaultDbDir = Path.Combine(appDataPath, "Daryva", "Database");
            if (!Directory.Exists(defaultDbDir))
                Directory.CreateDirectory(defaultDbDir);
            var defaultDbPath = Path.Combine(defaultDbDir, "DaryvaDB.db");
            return $"Data Source={defaultDbPath};";
        }

        /// <summary>
        /// Gets a configuration value by key from AppSettings.
        /// Checks app.config.local.json first, then falls back to app.config.json.
        /// </summary>
        public string? GetValue(string key)
        {
            // Check local config first (for sensitive data like SMTP credentials)
            if (_localSettings != null && _localSettings.TryGetValue(key, out var localValue))
            {
                return localValue;
            }
            
            // Fall back to default config
            if (_settings != null && _settings.TryGetValue(key, out var value))
            {
                return value;
            }

            return null;
        }

        /// <summary>
        /// Sets a configuration value in app.config.json.
        /// </summary>
        public void SetValue(string key, string value)
        {
            if (_settings == null)
                _settings = new Dictionary<string, string>();

            _settings[key] = value;
            SaveConfig();
        }

        /// <summary>
        /// Sets a configuration value in app.config.local.json.
        /// Creates the file if it doesn't exist.
        /// </summary>
        public void SetLocalValue(string key, string value)
        {
            if (_localSettings == null)
                _localSettings = new Dictionary<string, string>();

            _localSettings[key] = value;
            SaveLocalConfig();
        }

        /// <summary>
        /// Reloads the configuration files.
        /// </summary>
        public void ReloadLocalConfig()
        {
            LoadConfig();
        }

        private void SaveConfig()
        {
            try
            {
                var dir = Path.GetDirectoryName(ConfigPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var config = new ConfigFile
                {
                    AppSettings = _settings ?? new Dictionary<string, string>()
                };

                var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ConfigPath, json);
            }
            catch
            {
                // Silently fail
            }
        }

        private void SaveLocalConfig()
        {
            try
            {
                var dir = Path.GetDirectoryName(LocalConfigPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var config = new ConfigFile
                {
                    AppSettings = _localSettings ?? new Dictionary<string, string>(),
                    ConnectionStrings = _localConnectionString != null
                        ? new Dictionary<string, string> { [DefaultConnectionStringName] = _localConnectionString }
                        : new Dictionary<string, string>()
                };

                var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(LocalConfigPath, json);
            }
            catch
            {
                // Silently fail
            }
        }

        private class ConfigFile
        {
            public Dictionary<string, string>? AppSettings { get; set; }
            public Dictionary<string, string>? ConnectionStrings { get; set; }
        }
    }
}
