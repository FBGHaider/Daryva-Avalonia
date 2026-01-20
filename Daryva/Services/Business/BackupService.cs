using System.Data;
using System.IO;
using Dapper;
using Daryva.Services.Database;

namespace Daryva.Services.Business
{
    /// <summary>
    /// Service for database backup operations.
    /// </summary>
    public class BackupService : IBackupService
    {
        private readonly IDbContext _dbContext;
        private readonly ISettingsService _settingsService;

        public BackupService(IDbContext dbContext, ISettingsService settingsService)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        }

        public async Task<string> CreateBackupAsync(string? backupPath = null)
        {
            var databaseName = GetDatabaseName();
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var fileName = $"{databaseName}_Backup_{timestamp}.bak";

            if (string.IsNullOrWhiteSpace(backupPath))
            {
                var defaultLocation = await _settingsService.GetSettingAsync("BackupLocation", GetDefaultBackupLocation());
                backupPath = Path.Combine(defaultLocation, fileName);
            }
            else
            {
                backupPath = Path.Combine(backupPath, fileName);
            }

            // Ensure directory exists
            var directory = Path.GetDirectoryName(backupPath);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // SQL Server backup requires dynamic SQL (BACKUP DATABASE doesn't support parameters)
            var sql = $@"
                BACKUP DATABASE [{databaseName}]
                TO DISK = '{backupPath.Replace("'", "''")}'
                WITH FORMAT, INIT, NAME = '{databaseName} Full Backup', SKIP, NOREWIND, NOUNLOAD, STATS = 10";

            var connection = _dbContext.Connection;
            await connection.ExecuteAsync(sql);

            return backupPath;
        }

        public string GetDefaultBackupLocation()
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appDataPath, "Daryva", "Backups");
        }

        public string GetDatabaseName()
        {
            var connection = _dbContext.Connection;
            var dbName = connection.QueryFirstOrDefault<string>("SELECT DB_NAME()");
            return dbName ?? "DaryvaDB";
        }
    }
}
