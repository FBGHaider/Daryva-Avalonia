using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Dapper;
using FBGRentora.Services.Database;

namespace FBGRentora.Services.Data
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
                SELECT [SettingValue]
                FROM [dbo].[AppSettings]
                WHERE [SettingKey] = @Key";

            var connection = _dbContext.Connection;
            return await connection.QueryFirstOrDefaultAsync<string?>(sql, new { Key = key });
        }

        public async Task<Dictionary<string, string>> GetSettingsByCategoryAsync(string category)
        {
            const string sql = @"
                SELECT [SettingKey], [SettingValue]
                FROM [dbo].[AppSettings]
                WHERE [Category] = @Category";

            var connection = _dbContext.Connection;
            var results = await connection.QueryAsync<(string Key, string? Value)>(sql, new { Category = category });
            
            return results
                .Where(r => r.Value != null)
                .ToDictionary(r => r.Key, r => r.Value!);
        }

        public async Task<Dictionary<string, string>> GetAllSettingsAsync()
        {
            const string sql = @"
                SELECT [SettingKey], [SettingValue]
                FROM [dbo].[AppSettings]";

            var connection = _dbContext.Connection;
            var results = await connection.QueryAsync<(string Key, string? Value)>(sql);
            
            return results
                .Where(r => r.Value != null)
                .ToDictionary(r => r.Key, r => r.Value!);
        }

        public async Task SetSettingValueAsync(string key, string value, string settingType = "String")
        {
            const string sql = @"
                IF EXISTS (SELECT 1 FROM [dbo].[AppSettings] WHERE [SettingKey] = @Key)
                    UPDATE [dbo].[AppSettings]
                    SET [SettingValue] = @Value,
                        [UpdatedAt] = GETUTCDATE()
                    WHERE [SettingKey] = @Key
                ELSE
                    INSERT INTO [dbo].[AppSettings] ([SettingKey], [SettingValue], [SettingType], [Category], [UpdatedAt])
                    VALUES (@Key, @Value, @Type, 'General', GETUTCDATE())";

            var connection = _dbContext.Connection;
            await connection.ExecuteAsync(sql, new { Key = key, Value = value, Type = settingType });
        }

        public async Task SetSettingsAsync(Dictionary<string, string> settings)
        {
            const string sql = @"
                IF EXISTS (SELECT 1 FROM [dbo].[AppSettings] WHERE [SettingKey] = @Key)
                    UPDATE [dbo].[AppSettings]
                    SET [SettingValue] = @Value,
                        [UpdatedAt] = GETUTCDATE()
                    WHERE [SettingKey] = @Key
                ELSE
                    INSERT INTO [dbo].[AppSettings] ([SettingKey], [SettingValue], [SettingType], [Category], [UpdatedAt])
                    VALUES (@Key, @Value, 'String', 'General', GETUTCDATE())";

            var connection = _dbContext.Connection;
            await connection.ExecuteAsync(sql, settings.Select(s => new { Key = s.Key, Value = s.Value }));
        }

        public async Task DeleteSettingAsync(string key)
        {
            const string sql = @"
                DELETE FROM [dbo].[AppSettings]
                WHERE [SettingKey] = @Key";

            var connection = _dbContext.Connection;
            await connection.ExecuteAsync(sql, new { Key = key });
        }
    }
}
