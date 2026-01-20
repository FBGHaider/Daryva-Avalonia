using System.Globalization;
using Dapper;
using FBGRentora.Services.Data;
using FBGRentora.Services.Database;

namespace FBGRentora.Services.Business
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

        public async Task<decimal> GetDatabaseSizeAsync()
        {
            const string sql = @"
                SELECT 
                    CAST(SUM(size) * 8.0 / 1024.0 AS DECIMAL(18,2)) AS SizeMB
                FROM sys.master_files
                WHERE database_id = DB_ID()";

            try
            {
                var connection = _dbContext.Connection;
                var result = await connection.QueryFirstOrDefaultAsync<decimal?>(sql);
                return result ?? 0;
            }
            catch
            {
                return 0;
            }
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
