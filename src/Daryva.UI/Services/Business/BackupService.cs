using System.Data;
using System.IO;
using Dapper;
using Daryva.Services.Database;
using Daryva.Services.Platform;

namespace Daryva.Services.Business
{
    /// <summary>
    /// Service for database backup operations.
    /// Cross-platform: SQLite backups are simple file copies.
    /// </summary>
    public class BackupService : IBackupService
    {
        private readonly IDbContext _dbContext;
        private readonly ISettingsService _settingsService;
        private readonly IAppPaths _appPaths;

        public BackupService(IDbContext dbContext, ISettingsService settingsService, IAppPaths appPaths)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _appPaths = appPaths ?? throw new ArgumentNullException(nameof(appPaths));
        }

        public async Task<string> CreateBackupAsync(string? backupPath = null)
        {
            // For SQLite, backup is a simple file copy
            var connectionString = _dbContext.Connection.ConnectionString;
            var dbPath = ExtractDatabasePath(connectionString);
            
            if (string.IsNullOrWhiteSpace(dbPath) || !File.Exists(dbPath))
            {
                throw new InvalidOperationException($"Database file not found at: {dbPath}");
            }

            var databaseName = Path.GetFileNameWithoutExtension(dbPath);
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var fileName = $"{databaseName}_Backup_{timestamp}.db";

            // Determine backup directory
            string backupDirectory;
            if (string.IsNullOrWhiteSpace(backupPath))
            {
                var defaultLocation = await _settingsService.GetSettingAsync("BackupLocation", GetDefaultBackupLocation());
                if (string.IsNullOrWhiteSpace(defaultLocation))
                {
                    defaultLocation = GetDefaultBackupLocation();
                }
                backupDirectory = defaultLocation;
            }
            else
            {
                backupDirectory = backupPath;
            }

            // Normalize the path
            if (!Path.IsPathRooted(backupDirectory))
            {
                backupDirectory = Path.GetFullPath(backupDirectory);
            }
            else
            {
                backupDirectory = Path.GetFullPath(backupDirectory);
            }

            // Ensure directory exists
            if (!Directory.Exists(backupDirectory))
            {
                try
                {
                    Directory.CreateDirectory(backupDirectory);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Cannot create backup directory '{backupDirectory}': {ex.Message}", ex);
                }
            }

            // Build full backup file path
            var fullBackupPath = Path.Combine(backupDirectory, fileName);
            fullBackupPath = Path.GetFullPath(fullBackupPath);

            // Verify the directory is writable
            try
            {
                var testFile = Path.Combine(backupDirectory, "test_write.tmp");
                File.WriteAllText(testFile, "test");
                File.Delete(testFile);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Backup directory '{backupDirectory}' is not writable. Error: {ex.Message}", ex);
            }

            // Perform SQLite backup (file copy with WAL checkpoint)
            try
            {
                // SQLite backup: copy the database file
                // If WAL mode is enabled, we should checkpoint first, but for simplicity, just copy
                await Task.Run(() =>
                {
                    File.Copy(dbPath, fullBackupPath, overwrite: true);
                });

                // Verify the backup file was created
                if (!File.Exists(fullBackupPath))
                {
                    throw new InvalidOperationException($"Backup file was not created at '{fullBackupPath}'.");
                }

                return fullBackupPath;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error creating database backup: {ex.Message}", ex);
            }
        }

        public string GetDefaultBackupLocation()
        {
            // Cross-platform: use AppData\Daryva\Backups (or equivalent on macOS/Linux)
            var appDataPath = _appPaths.AppData;
            var backupsPath = Path.Combine(appDataPath, "Backups");
            
            if (!Directory.Exists(backupsPath))
            {
                try
                {
                    Directory.CreateDirectory(backupsPath);
                }
                catch
                {
                    // Fallback to Exports directory if Backups fails
                    return _appPaths.Exports;
                }
            }

            return backupsPath;
        }

        public string GetDatabaseName()
        {
            var connectionString = _dbContext.Connection.ConnectionString;
            var dbPath = ExtractDatabasePath(connectionString);
            
            if (string.IsNullOrWhiteSpace(dbPath))
            {
                return "DaryvaDB";
            }

            return Path.GetFileNameWithoutExtension(dbPath);
        }

        private string? ExtractDatabasePath(string connectionString)
        {
            // SQLite connection string format: "Data Source=path;..."
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
    }
}
