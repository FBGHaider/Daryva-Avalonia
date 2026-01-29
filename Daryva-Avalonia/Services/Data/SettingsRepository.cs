using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Daryva.Services.Database;

namespace Daryva.Services.Data
{
    /// <summary>
    /// Repository for application settings data access using Dapper.
    /// </summary>
    public class SettingsRepository : ISettingsRepository
    {
        private readonly IDbContext _dbContext;

        public SettingsRepository(IDbContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        public async Task<string?> GetSettingValueAsync(string key)
        {
            const string sql = @"
                SELECT SettingValue
                FROM AppSettings
                WHERE SettingKey = @Key";

            _dbContext.OpenConnection();
            var connection = _dbContext.Connection;
            return await connection.QueryFirstOrDefaultAsync<string?>(sql, new { Key = key });
        }

        public async Task<Dictionary<string, string>> GetSettingsByCategoryAsync(string category)
        {
            const string sql = @"
                SELECT SettingKey, SettingValue
                FROM AppSettings
                WHERE Category = @Category";

            _dbContext.OpenConnection();
            var connection = _dbContext.Connection;
            var results = await connection.QueryAsync<(string Key, string? Value)>(sql, new { Category = category });
            
            return results
                .Where(r => r.Value != null)
                .ToDictionary(r => r.Key, r => r.Value!);
        }

        public async Task<Dictionary<string, string>> GetAllSettingsAsync()
        {
            const string sql = @"
                SELECT SettingKey, SettingValue
                FROM AppSettings";

            _dbContext.OpenConnection();
            var connection = _dbContext.Connection;
            var results = await connection.QueryAsync<(string Key, string? Value)>(sql);
            
            return results
                .Where(r => r.Value != null)
                .ToDictionary(r => r.Key, r => r.Value!);
        }

        public async Task SetSettingValueAsync(string key, string value, string settingType = "String")
        {
            // SQLite doesn't support IF EXISTS in the same way, so use INSERT OR REPLACE or separate logic
            const string checkSql = @"
                SELECT COUNT(*) FROM AppSettings WHERE SettingKey = @Key";
            
            const string updateSql = @"
                UPDATE AppSettings
                SET SettingValue = @Value,
                    UpdatedAt = datetime('now')
                WHERE SettingKey = @Key";
            
            const string insertSql = @"
                INSERT INTO AppSettings (SettingKey, SettingValue, SettingType, Category, UpdatedAt)
                VALUES (@Key, @Value, @Type, 'General', datetime('now'))";

            _dbContext.OpenConnection();
            var connection = _dbContext.Connection;
            var exists = await connection.ExecuteScalarAsync<int>(checkSql, new { Key = key }) > 0;
            
            if (exists)
            {
                await connection.ExecuteAsync(updateSql, new { Key = key, Value = value });
            }
            else
            {
                await connection.ExecuteAsync(insertSql, new { Key = key, Value = value, Type = settingType });
            }
        }

        public async Task SetSettingsAsync(Dictionary<string, string> settings)
        {
            const string checkSql = @"
                SELECT COUNT(*) FROM AppSettings WHERE SettingKey = @Key";
            
            const string updateSql = @"
                UPDATE AppSettings
                SET SettingValue = @Value,
                    UpdatedAt = datetime('now')
                WHERE SettingKey = @Key";
            
            const string insertSql = @"
                INSERT INTO AppSettings (SettingKey, SettingValue, SettingType, Category, UpdatedAt)
                VALUES (@Key, @Value, 'String', 'General', datetime('now'))";

            _dbContext.OpenConnection();
            var connection = _dbContext.Connection;
            
            foreach (var setting in settings)
            {
                var exists = await connection.ExecuteScalarAsync<int>(checkSql, new { Key = setting.Key }) > 0;
                
                if (exists)
                {
                    await connection.ExecuteAsync(updateSql, new { Key = setting.Key, Value = setting.Value });
                }
                else
                {
                    await connection.ExecuteAsync(insertSql, new { Key = setting.Key, Value = setting.Value });
                }
            }
        }

        public async Task DeleteSettingAsync(string key)
        {
            const string sql = @"
                DELETE FROM AppSettings
                WHERE SettingKey = @Key";

            _dbContext.OpenConnection();
            var connection = _dbContext.Connection;
            await connection.ExecuteAsync(sql, new { Key = key });
        }
    }
}
