using System.Text.Json;

namespace Daryva.Services.Settings
{
    /// <summary>
    /// JSON-based settings store that persists to AppData.
    /// </summary>
    public class JsonSettingsStore : ISettingsStore
    {
        private readonly string _settingsFilePath;
        private Dictionary<string, string> _settings;

        public JsonSettingsStore()
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var appFolder = Path.Combine(appDataPath, "Daryva");
            
            if (!Directory.Exists(appFolder))
            {
                Directory.CreateDirectory(appFolder);
            }

            _settingsFilePath = Path.Combine(appFolder, "settings.json");
            _settings = LoadSettings();
        }

        public string? GetSetting(string key, string? defaultValue = null)
        {
            if (_settings.TryGetValue(key, out var value))
            {
                return value;
            }
            return defaultValue;
        }

        public void SetSetting(string key, string value)
        {
            _settings[key] = value;
            SaveSettings();
        }

        public void RemoveSetting(string key)
        {
            if (_settings.Remove(key))
            {
                SaveSettings();
            }
        }

        public void Clear()
        {
            _settings.Clear();
            SaveSettings();
        }

        private Dictionary<string, string> LoadSettings()
        {
            try
            {
                if (!File.Exists(_settingsFilePath))
                {
                    return new Dictionary<string, string>();
                }

                var json = File.ReadAllText(_settingsFilePath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return new Dictionary<string, string>();
                }

                return JsonSerializer.Deserialize<Dictionary<string, string>>(json) 
                    ?? new Dictionary<string, string>();
            }
            catch
            {
                // If file is corrupted or inaccessible, return empty dictionary
                return new Dictionary<string, string>();
            }
        }

        private void SaveSettings()
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };
                var json = JsonSerializer.Serialize(_settings, options);
                File.WriteAllText(_settingsFilePath, json);
            }
            catch
            {
                // Silently fail if unable to save settings
            }
        }
    }
}
