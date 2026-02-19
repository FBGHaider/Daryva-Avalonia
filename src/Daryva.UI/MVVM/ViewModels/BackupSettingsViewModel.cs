using System.IO;
using System.Windows.Input;
using Daryva.MVVM.Commands;
using Daryva.Services.Business;
using Daryva.Services.Dialog;

namespace Daryva.MVVM.ViewModels
{
    public class BackupSettingsViewModel : BaseViewModel
    {
        private readonly ISettingsService _settingsService;
        private readonly IBackupService _backupService;
        private readonly IDialogService _dialogService;

        private string _backupLocation = string.Empty;
        private bool _autoBackupEnabled = false;
        private string _autoBackupFrequency = "Daily";
        private int _backupRetentionCount = 30;
        private string _databaseType = "Unknown";
        private bool _isDatabaseConnected = false;
        private decimal _databaseSizeMB = 0;

        public BackupSettingsViewModel(
            ISettingsService settingsService,
            IBackupService backupService,
            IDialogService dialogService)
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _backupService = backupService ?? throw new ArgumentNullException(nameof(backupService));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));

            SaveCommand = new RelayCommand(async _ => await SaveAsync());
            ResetCommand = new RelayCommand(async _ => await LoadAsync());
            BackupNowCommand = new RelayCommand(async _ => await BackupNowAsync());
            RestoreBackupCommand = new RelayCommand(async _ => await RestoreBackupAsync());
            BrowseBackupLocationCommand = new RelayCommand(async _ => await BrowseBackupLocationAsync());

            _ = LoadAsync();
        }

        public ICommand SaveCommand { get; }
        public ICommand ResetCommand { get; }
        public ICommand BackupNowCommand { get; }
        public ICommand RestoreBackupCommand { get; }
        public ICommand BrowseBackupLocationCommand { get; }

        public string BackupLocation
        {
            get => _backupLocation;
            set => SetProperty(ref _backupLocation, value);
        }

        public bool AutoBackupEnabled
        {
            get => _autoBackupEnabled;
            set => SetProperty(ref _autoBackupEnabled, value);
        }

        public string AutoBackupFrequency
        {
            get => _autoBackupFrequency;
            set => SetProperty(ref _autoBackupFrequency, value);
        }

        public int BackupRetentionCount
        {
            get => _backupRetentionCount;
            set => SetProperty(ref _backupRetentionCount, value);
        }

        public string DatabaseType
        {
            get => _databaseType;
            set => SetProperty(ref _databaseType, value);
        }

        public bool IsDatabaseConnected
        {
            get => _isDatabaseConnected;
            set => SetProperty(ref _isDatabaseConnected, value);
        }

        public decimal DatabaseSizeMB
        {
            get => _databaseSizeMB;
            set => SetProperty(ref _databaseSizeMB, value);
        }

        public List<string> BackupFrequencyOptions { get; } = new() { "Daily", "Weekly" };

        private async Task BrowseBackupLocationAsync()
        {
            var selectedPath = await _dialogService.ShowFolderBrowserDialogAsync("Select folder for backups");
            if (!string.IsNullOrEmpty(selectedPath))
            {
                BackupLocation = selectedPath;
            }
        }

        private async Task LoadAsync()
        {
            try
            {
                BackupLocation = await _settingsService.GetSettingAsync("BackupLocation", _backupService.GetDefaultBackupLocation()) ?? _backupService.GetDefaultBackupLocation();
                AutoBackupEnabled = await _settingsService.GetSettingAsync<bool>("AutoBackupEnabled", false) ?? false;
                AutoBackupFrequency = await _settingsService.GetSettingAsync("AutoBackupFrequency", "Daily") ?? "Daily";
                BackupRetentionCount = await _settingsService.GetSettingAsync<int>("BackupRetentionCount", 30) ?? 30;

                DatabaseType = "Cloud";
                IsDatabaseConnected = await _settingsService.IsDatabaseConnectedAsync();
                DatabaseSizeMB = await _settingsService.GetDatabaseSizeAsync();
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error loading settings: {ex.Message}", "Error");
            }
        }

        private async Task SaveAsync()
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(BackupLocation) && !Directory.Exists(BackupLocation))
                {
                    _dialogService.ShowMessage("Backup location does not exist. Please select a valid folder.", "Validation Error");
                    return;
                }

                if (BackupRetentionCount < 1)
                {
                    _dialogService.ShowMessage("Backup retention count must be at least 1.", "Validation Error");
                    return;
                }

                await _settingsService.SetSettingAsync("BackupLocation", BackupLocation);
                await _settingsService.SetSettingAsync("AutoBackupEnabled", AutoBackupEnabled);
                await _settingsService.SetSettingAsync("AutoBackupFrequency", AutoBackupFrequency);
                await _settingsService.SetSettingAsync("BackupRetentionCount", BackupRetentionCount);

                _dialogService.ShowMessage("Settings saved successfully. Changes will apply to future actions only.", "Settings Saved");
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error saving settings: {ex.Message}", "Error");
            }
        }

        private async Task BackupNowAsync()
        {
            try
            {
                var backupPath = string.IsNullOrWhiteSpace(BackupLocation) 
                    ? _backupService.GetDefaultBackupLocation() 
                    : BackupLocation;

                if (!Directory.Exists(backupPath))
                {
                    Directory.CreateDirectory(backupPath);
                }
                
                var result = await _backupService.CreateBackupAsync(backupPath);
                
                _dialogService.ShowMessage($"Backup created successfully!\n\nLocation: {result}", "Backup Complete");
            }
            catch (InvalidOperationException ex)
            {
                // Provide more helpful error message for permission issues
                var errorMsg = ex.Message;
                if (errorMsg.Contains("permission") || errorMsg.Contains("Permission"))
                {
                    errorMsg += "\n\nTip: Ensure the backup location folder exists and you have write permissions to it.";
                }
                _dialogService.ShowMessage($"Error creating backup: {errorMsg}", "Backup Error");
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error creating backup: {ex.Message}", "Backup Error");
            }
        }

        private async Task RestoreBackupAsync()
        {
            try
            {
                var backupFilePath = await _dialogService.ShowOpenFileDialogAsync(
                    "JSON files|*.json|All files|*.*",
                    "Select backup JSON file");

                if (string.IsNullOrWhiteSpace(backupFilePath))
                {
                    return;
                }

                var confirmed = await _dialogService.ShowConfirmationAsync(
                    "This will import the selected backup JSON into the currently selected organization. Continue?",
                    "Restore Backup");

                if (!confirmed)
                {
                    return;
                }

                await _dialogService.RunWithProgressAsync(
                    "Restore Backup",
                    "Importing backup data into API...",
                    async () => await _backupService.RestoreBackupAsync(backupFilePath));

                _dialogService.ShowMessage("Backup restored successfully.", "Restore Complete");
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error restoring backup: {ex.Message}", "Restore Error");
            }
        }
    }
}
