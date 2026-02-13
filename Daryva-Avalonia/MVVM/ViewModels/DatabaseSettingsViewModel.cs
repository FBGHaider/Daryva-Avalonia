using System.IO;
using System.Windows.Input;
using Daryva.MVVM.Commands;
using Daryva.Services;
using Daryva.Services.Dialog;

namespace Daryva.MVVM.ViewModels
{
    public class DatabaseSettingsViewModel : BaseViewModel
    {
        private const string DatabasePathOverrideKey = "DatabasePathOverride";

        private readonly IConfigurationService _configurationService;
        private readonly IDialogService _dialogService;

        private string _currentDatabasePath = string.Empty;
        private bool _hasOverride;

        public DatabaseSettingsViewModel(
            IConfigurationService configurationService,
            IDialogService dialogService)
        {
            _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));

            ConnectToAnotherDatabaseCommand = new RelayCommand(async _ => await ConnectToAnotherDatabaseAsync());
            UseDefaultDatabaseCommand = new RelayCommand(_ => UseDefaultDatabase());
            ExportDatabaseCommand = new RelayCommand(async _ => await ExportDatabaseAsync());

            RefreshCurrentPath();
        }

        public ICommand ConnectToAnotherDatabaseCommand { get; }
        public ICommand UseDefaultDatabaseCommand { get; }
        public ICommand ExportDatabaseCommand { get; }

        public string CurrentDatabasePath
        {
            get => _currentDatabasePath;
            set => SetProperty(ref _currentDatabasePath, value);
        }

        public bool HasOverride
        {
            get => _hasOverride;
            set => SetProperty(ref _hasOverride, value);
        }

        private void RefreshCurrentPath()
        {
            CurrentDatabasePath = _configurationService.GetCurrentDatabasePath();
            if (string.IsNullOrWhiteSpace(CurrentDatabasePath))
                CurrentDatabasePath = "(Not set)";
            HasOverride = !string.IsNullOrWhiteSpace(_configurationService.GetValue(DatabasePathOverrideKey));
        }

        private async Task ConnectToAnotherDatabaseAsync()
        {
            var path = await _dialogService.ShowOpenFileDialogAsync(
                "Database files|*.db|All files|*.*",
                "Select database file (DaryvaDB.db)");
            if (string.IsNullOrWhiteSpace(path))
                return;

            if (!File.Exists(path))
            {
                _dialogService.ShowMessage("The selected file does not exist.", "Invalid path");
                return;
            }

            var dir = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(dir))
            {
                _dialogService.ShowMessage("Could not determine the folder.", "Invalid path");
                return;
            }

            _configurationService.SetLocalValue(DatabasePathOverrideKey, path);
            _configurationService.ReloadLocalConfig();
            RefreshCurrentPath();
            _dialogService.ShowMessage(
                "Database path updated. Restart the app to use this database.",
                "Connect to database");
        }

        private void UseDefaultDatabase()
        {
            _configurationService.SetLocalValue(DatabasePathOverrideKey, "");
            _configurationService.ReloadLocalConfig();
            RefreshCurrentPath();
            _dialogService.ShowMessage(
                "Switched back to the default database. Restart the app for the change to take effect.",
                "Default database");
        }

        private async Task ExportDatabaseAsync()
        {
            var sourcePath = _configurationService.GetCurrentDatabasePath();
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            {
                _dialogService.ShowMessage("Current database file not found or path is not set.", "Export");
                return;
            }

            var defaultName = $"DaryvaDB_export_{DateTime.Now:yyyyMMdd_HHmmss}.db";
            var savePath = await Task.Run(() =>
                _dialogService.ShowSaveFileDialog(defaultName, "Database files|*.db|All files|*.*", "Export database"));
            if (string.IsNullOrWhiteSpace(savePath))
                return;

            try
            {
                File.Copy(sourcePath, savePath, overwrite: true);
                _dialogService.ShowMessage($"Database exported to:\n{savePath}", "Export complete");
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Export failed: {ex.Message}", "Export error");
            }
        }
    }
}
