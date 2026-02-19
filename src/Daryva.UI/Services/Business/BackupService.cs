using System.IO;
using System.Net.Http;
using System.Text;
using Daryva.Services.Api;
using Daryva.Services.Platform;

namespace Daryva.Services.Business
{
    /// <summary>
    /// Service for database backup operations.
    /// Cross-platform: SQLite backups are simple file copies.
    /// </summary>
    public class BackupService : IBackupService
    {
        private readonly ISettingsService _settingsService;
        private readonly IAppPaths _appPaths;
        private readonly IApiClient _apiClient;

        public BackupService(ISettingsService settingsService, IAppPaths appPaths, IApiClient apiClient)
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _appPaths = appPaths ?? throw new ArgumentNullException(nameof(appPaths));
            _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        }

        public async Task<string> CreateBackupAsync(string? backupPath = null)
        {
            var backupDirectory = await ResolveBackupDirectoryAsync(backupPath);

            if (!_apiClient.CurrentOrgId.HasValue)
            {
                throw new InvalidOperationException("An organization must be selected before creating a backup.");
            }

            return await CreateApiBackupAsync(backupDirectory);
        }

        public async Task RestoreBackupAsync(string backupFilePath)
        {
            if (string.IsNullOrWhiteSpace(backupFilePath))
            {
                throw new InvalidOperationException("Backup file path is required.");
            }

            var fullPath = Path.GetFullPath(backupFilePath);
            if (!File.Exists(fullPath))
            {
                throw new InvalidOperationException($"Backup file not found at '{fullPath}'.");
            }

            if (!_apiClient.CurrentOrgId.HasValue)
            {
                throw new InvalidOperationException("An organization must be selected before restoring a backup.");
            }

            var json = await File.ReadAllTextAsync(fullPath);
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new InvalidOperationException("Backup file is empty.");
            }

            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _apiClient.HttpClient.PostAsync("api/import", content);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                if (string.IsNullOrWhiteSpace(error))
                {
                    error = response.ReasonPhrase ?? "Unknown error";
                }

                throw new InvalidOperationException(
                    $"Restore failed ({(int)response.StatusCode} {response.StatusCode}): {error}");
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
            return "Daryva Cloud";
        }

        private async Task<string> ResolveBackupDirectoryAsync(string? backupPath)
        {
            string backupDirectory;
            if (string.IsNullOrWhiteSpace(backupPath))
            {
                var orgScopedBackupKey = GetOrgScopedSettingKey("BackupLocation");
                var defaultLocation = await _settingsService.GetSettingAsync(orgScopedBackupKey, GetDefaultBackupLocation());
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

            backupDirectory = Path.GetFullPath(backupDirectory);

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

            return backupDirectory;
        }

        private string GetOrgScopedSettingKey(string key)
        {
            var orgId = _apiClient.CurrentOrgId;
            return orgId.HasValue ? $"Org.{orgId.Value:N}.{key}" : key;
        }

        private async Task<string> CreateApiBackupAsync(string backupDirectory)
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var fileName = $"Daryva_Api_Backup_{timestamp}.json";
            var fullBackupPath = Path.GetFullPath(Path.Combine(backupDirectory, fileName));

            var response = await _apiClient.HttpClient.GetAsync("api/backup/export");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            await File.WriteAllTextAsync(fullBackupPath, json);

            if (!File.Exists(fullBackupPath))
            {
                throw new InvalidOperationException($"Backup file was not created at '{fullBackupPath}'.");
            }

            return fullBackupPath;
        }

    }
}
