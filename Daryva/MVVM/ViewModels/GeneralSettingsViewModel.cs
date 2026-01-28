using System.Windows.Input;
using Daryva.MVVM.Commands;
using Daryva.Services;
using Daryva.Services.Business;
using Daryva.Services.Dialog;

namespace Daryva.MVVM.ViewModels
{
    public class GeneralSettingsViewModel : BaseViewModel
    {
        private readonly ISettingsService _settingsService;
        private readonly IDialogService _dialogService;

        private string _currency = "GBP";
        private string _dateFormat = "dd/MM/yyyy";
        private string _timeZone = "GMT Standard Time";
        private string _appStartPage = "Dashboard";
        private bool _confirmDestructiveActions = true;
        private bool _autoRefreshDashboard = true;

        public GeneralSettingsViewModel(ISettingsService settingsService, IDialogService dialogService)
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));

            SaveCommand = new RelayCommand(async _ => await SaveAsync());
            ResetCommand = new RelayCommand(async _ => await LoadAsync());

            _ = LoadAsync();
        }

        public ICommand SaveCommand { get; }
        public ICommand ResetCommand { get; }

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
    }
}
