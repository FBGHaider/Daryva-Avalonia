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

            // Normalize the path - ensure it's absolute and properly formatted
            if (!Path.IsPathRooted(backupDirectory))
            {
                // If relative path, make it absolute
                backupDirectory = Path.GetFullPath(backupDirectory);
            }
            else
            {
                // If absolute path, normalize it (removes trailing slashes, etc.)
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

            // Ensure the full path is properly formatted (absolute)
            fullBackupPath = Path.GetFullPath(fullBackupPath);

            // Verify the directory is writable (test write permission)
            try
            {
                var testFile = Path.Combine(backupDirectory, "test_write.tmp");
                File.WriteAllText(testFile, "test");
                File.Delete(testFile);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Backup directory '{backupDirectory}' is not writable. SQL Server service account may not have permissions. Error: {ex.Message}", ex);
            }

            // Ensure connection is open
            _dbContext.OpenConnection();
            var connection = _dbContext.Connection;

            if (connection.State != System.Data.ConnectionState.Open)
            {
                throw new InvalidOperationException("Database connection is not open. Cannot perform backup.");
            }

            try
            {
                // SQL Server backup requires dynamic SQL (BACKUP DATABASE doesn't support parameters)
                // Escape single quotes in the path
                var escapedPath = fullBackupPath.Replace("'", "''");
                var sql = $@"
                    BACKUP DATABASE [{databaseName}]
                    TO DISK = '{escapedPath}'
                    WITH FORMAT, INIT, NAME = '{databaseName} Full Backup', SKIP, NOREWIND, NOUNLOAD, STATS = 10";

                await connection.ExecuteAsync(sql);

                // Verify the backup file was actually created
                if (!File.Exists(fullBackupPath))
                {
                    throw new InvalidOperationException($"Backup command completed but backup file was not found at '{fullBackupPath}'. SQL Server may not have write permissions to this location.");
                }

                return fullBackupPath;
            }
            catch (Exception ex)
            {
                // Provide more detailed error information
                if (ex.Message.Contains("permission") || ex.Message.Contains("access") || ex.Message.Contains("Access is denied") || ex.Message.Contains("error 5"))
                {
                    var sqlServiceAccount = GetSqlServerServiceAccount();
                    var helpMessage = $"\n\nTo fix this, run the enhanced PowerShell script as Administrator:\n" +
                        $"   .\\fix-sql-backup-permissions-enhanced.ps1\n\n" +
                        $"This script will:\n" +
                        $"1. Detect your SQL Server service account\n" +
                        $"2. Set up C:\\Backups\\Daryva with proper permissions\n" +
                        $"3. Grant permissions with correct inheritance flags\n" +
                        $"4. Optionally restart SQL Server service to apply changes\n\n" +
                        $"IMPORTANT: After running the script, you may need to:\n" +
                        $"1. Restart the SQL Server service (the script can do this)\n" +
                        $"2. Set backup location to: C:\\Backups\\Daryva\n" +
                        $"3. Try creating a backup again";
                    
                    throw new InvalidOperationException($"SQL Server does not have permission to write to '{fullBackupPath}'.{helpMessage}", ex);
                }
                throw new InvalidOperationException($"Error creating database backup: {ex.Message}", ex);
            }
        }

        public string GetDefaultBackupLocation()
        {
            // Prefer C:\Backups\Daryva first (easier to manage permissions)
            var preferredPath = @"C:\Backups\Daryva";
            try
            {
                if (!Directory.Exists(preferredPath))
                {
                    Directory.CreateDirectory(preferredPath);
                }
                // Test write permission (from application context)
                var testFile = Path.Combine(preferredPath, "test_write.tmp");
                File.WriteAllText(testFile, "test");
                File.Delete(testFile);
                return preferredPath;
            }
            catch
            {
                // If C:\Backups\Daryva fails, try SQL Server default backup directory
            }

            // Try SQL Server default backup directory as fallback
            // Check both MSSQLSERVER and SQLEXPRESS instances
            var sqlServerBackupPaths = new[]
            {
                // SQL Server 2022
                @"C:\Program Files\Microsoft SQL Server\MSSQL16.MSSQLSERVER\MSSQL\Backup",
                @"C:\Program Files\Microsoft SQL Server\MSSQL16.SQLEXPRESS\MSSQL\Backup",
                // SQL Server 2019
                @"C:\Program Files\Microsoft SQL Server\MSSQL15.MSSQLSERVER\MSSQL\Backup",
                @"C:\Program Files\Microsoft SQL Server\MSSQL15.SQLEXPRESS\MSSQL\Backup",
                // SQL Server 2017
                @"C:\Program Files\Microsoft SQL Server\MSSQL14.MSSQLSERVER\MSSQL\Backup",
                @"C:\Program Files\Microsoft SQL Server\MSSQL14.SQLEXPRESS\MSSQL\Backup",
                // SQL Server 2016
                @"C:\Program Files\Microsoft SQL Server\MSSQL13.MSSQLSERVER\MSSQL\Backup",
                @"C:\Program Files\Microsoft SQL Server\MSSQL13.SQLEXPRESS\MSSQL\Backup",
            };

            foreach (var path in sqlServerBackupPaths)
            {
                if (Directory.Exists(path))
                {
                    var daryvaPath = Path.Combine(path, "Daryva");
                    if (!Directory.Exists(daryvaPath))
                    {
                        try
                        {
                            Directory.CreateDirectory(daryvaPath);
                        }
                        catch { continue; }
                    }
                    return daryvaPath;
                }
            }

            // Last resort: AppData (user will need to grant SQL Server permissions)
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appDataPath, "Daryva", "Backups");
        }

        public string GetDatabaseName()
        {
            var connection = _dbContext.Connection;
            var dbName = connection.QueryFirstOrDefault<string>("SELECT DB_NAME()");
            return dbName ?? "DaryvaDB";
        }

        private string GetSqlServerServiceAccount()
        {
            try
            {
                // Try to get SQL Server service account from registry or service
                // Default common accounts
                return "NT SERVICE\\MSSQLSERVER or NT AUTHORITY\\SYSTEM";
            }
            catch
            {
                return "SQL Server service account";
            }
        }
    }
}
