using System.Windows.Input;
using Daryva.MVVM.Commands;
using Daryva.Services;
using Daryva.Services.Business;
using Daryva.Services.Dialog;
using Daryva.Services.Update;

namespace Daryva.MVVM.ViewModels
{
    public class GeneralSettingsViewModel : BaseViewModel
    {
        private readonly ISettingsService _settingsService;
        private readonly IDialogService _dialogService;
        private readonly IUpdateService _updateService;
        private readonly IPaymentService _paymentService;

        private string _currency = "GBP";
        private string _dateFormat = "dd/MM/yyyy";
        private string _timeZone = "GMT Standard Time";
        private string _appStartPage = "Dashboard";
        private bool _confirmDestructiveActions = true;
        private bool _autoRefreshDashboard = true;

        private string _updateStatus = "";
        private string? _availableUpdateVersion;
        private bool _isCheckingForUpdates;
        private bool _isInstallingUpdate;

        public GeneralSettingsViewModel(ISettingsService settingsService, IDialogService dialogService, IUpdateService updateService, IPaymentService paymentService)
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _updateService = updateService ?? throw new ArgumentNullException(nameof(updateService));
            _paymentService = paymentService ?? throw new ArgumentNullException(nameof(paymentService));

            SaveCommand = new RelayCommand(async _ => await SaveAsync());
            ResetCommand = new RelayCommand(async _ => await LoadAsync());
            CheckForUpdatesCommand = new RelayCommand(async _ => await CheckForUpdatesAsync(), _ => !IsCheckingForUpdates && !IsInstallingUpdate);
            InstallAndRestartCommand = new RelayCommand(async _ => await InstallAndRestartAsync(), _ => !string.IsNullOrEmpty(AvailableUpdateVersion) && !IsInstallingUpdate);
            CleanupDuplicateChargesCommand = new RelayCommand(async _ => await CleanupDuplicateChargesAsync(), _ => ConfirmDangerZoneChecked && !IsCleaningUpDuplicateCharges);

            _ = LoadAsync();
            InitializeUpdateStatus();
        }

        public ICommand SaveCommand { get; }
        public ICommand ResetCommand { get; }
        public ICommand CheckForUpdatesCommand { get; }
        public ICommand InstallAndRestartCommand { get; }
        public ICommand CleanupDuplicateChargesCommand { get; }

        public string Currency
        {
            get => _currency;
            set => SetProperty(ref _currency, value);
        }

        public string DateFormat
        {
            get => _dateFormat;
            set => SetProperty(ref _dateFormat, value);
        }

        public string TimeZone
        {
            get => _timeZone;
            set => SetProperty(ref _timeZone, value);
        }

        public string AppStartPage
        {
            get => _appStartPage;
            set => SetProperty(ref _appStartPage, value);
        }

        public bool ConfirmDestructiveActions
        {
            get => _confirmDestructiveActions;
            set => SetProperty(ref _confirmDestructiveActions, value);
        }

        public bool AutoRefreshDashboard
        {
            get => _autoRefreshDashboard;
            set => SetProperty(ref _autoRefreshDashboard, value);
        }

        public List<string> AppStartPageOptions { get; } = new() { "Dashboard", "Houses", "Rent" };
        public List<string> DateFormatOptions { get; } = new() { "dd/MM/yyyy", "MM/dd/yyyy", "yyyy-MM-dd", "dd MMM yyyy" };

        public string CurrentVersion => _updateService.CurrentVersion;
        public bool IsUpdateCheckAvailable => _updateService.IsInstalled;

        public string UpdateStatus
        {
            get => _updateStatus;
            set => SetProperty(ref _updateStatus, value);
        }

        public string? AvailableUpdateVersion
        {
            get => _availableUpdateVersion;
            set => SetProperty(ref _availableUpdateVersion, value, RaiseUpdateCommandsCanExecute);
        }

        public bool IsCheckingForUpdates
        {
            get => _isCheckingForUpdates;
            set => SetProperty(ref _isCheckingForUpdates, value, RaiseUpdateCommandsCanExecute);
        }

        public bool IsInstallingUpdate
        {
            get => _isInstallingUpdate;
            set => SetProperty(ref _isInstallingUpdate, value, RaiseUpdateCommandsCanExecute);
        }

        private bool _isCleaningUpDuplicateCharges;
        public bool IsCleaningUpDuplicateCharges
        {
            get => _isCleaningUpDuplicateCharges;
            set => SetProperty(ref _isCleaningUpDuplicateCharges, value, () => ((Commands.RelayCommand)CleanupDuplicateChargesCommand).RaiseCanExecuteChanged());
        }

        private bool _confirmDangerZoneChecked;
        /// <summary>User must check this to enable the "Clean up duplicate rent charges" button.</summary>
        public bool ConfirmDangerZoneChecked
        {
            get => _confirmDangerZoneChecked;
            set => SetProperty(ref _confirmDangerZoneChecked, value, () => ((Commands.RelayCommand)CleanupDuplicateChargesCommand).RaiseCanExecuteChanged());
        }

        private void RaiseUpdateCommandsCanExecute()
        {
            ((Commands.RelayCommand)CheckForUpdatesCommand).RaiseCanExecuteChanged();
            ((Commands.RelayCommand)InstallAndRestartCommand).RaiseCanExecuteChanged();
        }

        private async Task LoadAsync()
        {
            try
            {
                Currency = await _settingsService.GetSettingAsync("Currency", "GBP") ?? "GBP";
                DateFormat = await _settingsService.GetSettingAsync("DateFormat", "dd/MM/yyyy") ?? "dd/MM/yyyy";
                TimeZone = await _settingsService.GetSettingAsync("TimeZone", "GMT Standard Time") ?? "GMT Standard Time";
                AppStartPage = await _settingsService.GetSettingAsync("AppStartPage", "Dashboard") ?? "Dashboard";
                ConfirmDestructiveActions = await _settingsService.GetSettingAsync<bool>("ConfirmDestructiveActions", true) ?? true;
                AutoRefreshDashboard = await _settingsService.GetSettingAsync<bool>("AutoRefreshDashboard", true) ?? true;
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
                await _settingsService.SetSettingAsync("Currency", Currency);
                await _settingsService.SetSettingAsync("DateFormat", DateFormat);
                await _settingsService.SetSettingAsync("TimeZone", TimeZone);
                await _settingsService.SetSettingAsync("AppStartPage", AppStartPage);
                await _settingsService.SetSettingAsync("ConfirmDestructiveActions", ConfirmDestructiveActions);
                await _settingsService.SetSettingAsync("AutoRefreshDashboard", AutoRefreshDashboard);
                DateTimeFormatProvider.DateFormat = DateFormat;

                _dialogService.ShowMessage("Settings saved successfully. Changes will apply to future actions only.", "Settings Saved");
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error saving settings: {ex.Message}", "Error");
            }
        }

        private void InitializeUpdateStatus()
        {
            if (!_updateService.IsInstalled)
            {
                UpdateStatus = "Updates not available (running from source or portable build).";
                return;
            }
            UpdateStatus = $"Current version: {_updateService.CurrentVersion}";
        }

        private async Task CheckForUpdatesAsync()
        {
            if (!_updateService.IsInstalled)
            {
                UpdateStatus = "Updates not available.";
                return;
            }

            IsCheckingForUpdates = true;
            AvailableUpdateVersion = null;
            UpdateStatus = "Checking for updates...";

            try
            {
                var update = await _updateService.CheckAsync();
                if (update != null)
                {
                    AvailableUpdateVersion = update.Version;
                    UpdateStatus = $"Update available: {update.Version}";
                }
                else
                {
                    UpdateStatus = "Up to date";
                }
            }
            catch (Exception ex)
            {
                UpdateStatus = "Update check failed.";
                _dialogService.ShowMessage(
                    "Update check failed. Possible causes:\n\n• GitHub rate limit (60 requests/hour). Try again later or add UpdateFeedToken in AppData\\Daryva\\app.config.local.json.\n• Release missing assets: upload releases.win.json and FBGHaider.Daryva-X.Y.Z-full.nupkg to the release.\n• Release tag must be exactly vX.Y.Z (e.g. v1.0.1).",
                    "Check for updates");
                System.Diagnostics.Debug.WriteLine($"[Update] Check failed: {ex.Message}");
            }
            finally
            {
                IsCheckingForUpdates = false;
            }
        }

        private async Task InstallAndRestartAsync()
        {
            if (string.IsNullOrEmpty(AvailableUpdateVersion))
                return;

            IsInstallingUpdate = true;
            UpdateStatus = "Downloading update...";

            try
            {
                var progress = new Progress<int>(p => UpdateStatus = $"Downloading update... {p}%");
                await _updateService.DownloadAndApplyAsync(progress);
                // ApplyUpdatesAndRestart exits the app - we never reach here
            }
            catch (Exception ex)
            {
                UpdateStatus = "Update failed.";
                _dialogService.ShowMessage($"Update failed: {ex.Message}", "Update Error");
                System.Diagnostics.Debug.WriteLine($"[Update] Install failed: {ex.Message}");
            }
            finally
            {
                IsInstallingUpdate = false;
            }
        }

        private async Task CleanupDuplicateChargesAsync()
        {
            IsCleaningUpDuplicateCharges = true;
            try
            {
                var removed = await _paymentService.CleanupDuplicateRentChargesAsync();
                if (removed > 0)
                    _dialogService.ShowMessage($"Cleanup complete. Removed {removed} duplicate rent charge(s). The rent ledger will show one row per tenant per period.", "Data Cleanup");
                else
                    _dialogService.ShowMessage("No duplicate rent charges found. Your data is already clean.", "Data Cleanup");
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Cleanup failed: {ex.Message}", "Error");
            }
            finally
            {
                IsCleaningUpDuplicateCharges = false;
            }
        }
    }
}
