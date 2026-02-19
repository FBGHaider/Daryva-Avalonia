using System.Globalization;
using System.IO;
using Dapper;
using Daryva.Services.Data;
using Daryva.Services.Database;

namespace Daryva.Services.Business
{
    /// <summary>
    /// Service for managing application settings.
    /// </summary>
    public class SettingsService : ISettingsService
    {
        private readonly ISettingsRepository _settingsRepository;
        private readonly IDbContext _dbContext;

        public SettingsService(ISettingsRepository settingsRepository, IDbContext dbContext)
        {
            _settingsRepository = settingsRepository ?? throw new ArgumentNullException(nameof(settingsRepository));
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        public async Task<T?> GetSettingAsync<T>(string key, T? defaultValue = default) where T : struct
        {
            var value = await _settingsRepository.GetSettingValueAsync(key);
            if (string.IsNullOrWhiteSpace(value))
                return defaultValue;

            try
            {
                if (typeof(T) == typeof(bool))
                {
                    return (T)(object)bool.Parse(value);
                }
                else if (typeof(T) == typeof(int))
                {
                    return (T)(object)int.Parse(value);
                }
                else if (typeof(T) == typeof(decimal))
                {
                    return (T)(object)decimal.Parse(value);
                }
                else if (typeof(T) == typeof(DateTime))
                {
                    return (T)(object)DateTime.Parse(value);
                }
                else if (typeof(T).IsEnum)
                {
                    return (T)Enum.Parse(typeof(T), value);
                }
            }
            catch
            {
                return defaultValue;
            }

            return defaultValue;
        }

        public async Task<string?> GetSettingAsync(string key, string? defaultValue = null)
        {
            var value = await _settingsRepository.GetSettingValueAsync(key);
            return string.IsNullOrWhiteSpace(value) ? defaultValue : value;
        }

        public async Task<Dictionary<string, string>> GetCategorySettingsAsync(string category)
        {
            return await _settingsRepository.GetSettingsByCategoryAsync(category);
        }

        public async Task SetSettingAsync<T>(string key, T value)
        {
            string stringValue = value switch
            {
                bool b => b.ToString().ToLowerInvariant(),
                int i => i.ToString(),
                decimal d => d.ToString(CultureInfo.InvariantCulture),
                DateTime dt => dt.ToString("O"),
                Enum e => e.ToString(),
                _ => value?.ToString() ?? string.Empty
            };

            string settingType = value switch
            {
                bool => "Bool",
                int => "Int",
                decimal => "Decimal",
                DateTime => "DateTime",
                _ => "String"
            };

            await _settingsRepository.SetSettingValueAsync(key, stringValue, settingType);
        }

        public async Task SetSettingsAsync(Dictionary<string, string> settings)
        {
            await _settingsRepository.SetSettingsAsync(settings);
        }

        public Task<decimal> GetDatabaseSizeAsync()
        {
            try
            {
                var connectionString = _dbContext.Connection.ConnectionString;
                
                // SQLite: Get file size from database file
                var dbPath = ExtractDatabasePath(connectionString);
                if (!string.IsNullOrWhiteSpace(dbPath) && File.Exists(dbPath))
                {
                    var fileInfo = new FileInfo(dbPath);
                    var sizeMB = (decimal)fileInfo.Length / (1024 * 1024);
                    return Task.FromResult(Math.Round(sizeMB, 2));
                }
                return Task.FromResult(0m);
            }
            catch
            {
                return Task.FromResult(0m);
            }
        }

        private string? ExtractDatabasePath(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                return null;

            var parts = connectionString.Split(';');
            foreach (var part in parts)
            {
                var trimmed = part.Trim();
                if (trimmed.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.StartsWith("DataSource=", StringComparison.OrdinalIgnoreCase))
                {
                    var path = trimmed.Substring(trimmed.IndexOf('=') + 1).Trim();
                    return path;
                }
            }

            return null;
        }

        public async Task<bool> IsDatabaseConnectedAsync()
        {
            try
            {
                var connection = _dbContext.Connection;
                await connection.QueryFirstAsync<int>("SELECT 1");
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
