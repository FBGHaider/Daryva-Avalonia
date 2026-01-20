using System.Windows.Input;
using FBGRentora.MVVM.Commands;
using FBGRentora.Services.Business;
using FBGRentora.Services.Dialog;

namespace FBGRentora.MVVM.ViewModels
{
    public class RentSettingsViewModel : BaseViewModel
    {
        private readonly ISettingsService _settingsService;
        private readonly IDialogService _dialogService;

        private int _defaultRentDueDay = 1;
        private int _rentGracePeriodDays = 3;
        private bool _allowPartialPayments = true;
        private bool _allowOverpayment = true;

        public RentSettingsViewModel(ISettingsService settingsService, IDialogService dialogService)
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));

            SaveCommand = new RelayCommand(async _ => await SaveAsync());
            ResetCommand = new RelayCommand(async _ => await LoadAsync());

            _ = LoadAsync();
        }

        public ICommand SaveCommand { get; }
        public ICommand ResetCommand { get; }

        public int DefaultRentDueDay
        {
            get => _defaultRentDueDay;
            set => SetProperty(ref _defaultRentDueDay, value);
        }

        public int RentGracePeriodDays
        {
            get => _rentGracePeriodDays;
            set => SetProperty(ref _rentGracePeriodDays, value);
        }

        public bool AllowPartialPayments
        {
            get => _allowPartialPayments;
            set => SetProperty(ref _allowPartialPayments, value);
        }

        public bool AllowOverpayment
        {
            get => _allowOverpayment;
            set => SetProperty(ref _allowOverpayment, value);
        }

        private async Task LoadAsync()
        {
            try
            {
                DefaultRentDueDay = await _settingsService.GetSettingAsync<int>("DefaultRentDueDay", 1) ?? 1;
                RentGracePeriodDays = await _settingsService.GetSettingAsync<int>("RentGracePeriodDays", 3) ?? 3;
                AllowPartialPayments = await _settingsService.GetSettingAsync<bool>("AllowPartialPayments", true) ?? true;
                AllowOverpayment = await _settingsService.GetSettingAsync<bool>("AllowOverpayment", true) ?? true;
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
                if (DefaultRentDueDay < 1 || DefaultRentDueDay > 28)
                {
                    _dialogService.ShowMessage("Rent due day must be between 1 and 28.", "Validation Error");
                    return;
                }

                if (RentGracePeriodDays < 0)
                {
                    _dialogService.ShowMessage("Grace period cannot be negative.", "Validation Error");
                    return;
                }

                await _settingsService.SetSettingAsync("DefaultRentDueDay", DefaultRentDueDay);
                await _settingsService.SetSettingAsync("RentGracePeriodDays", RentGracePeriodDays);
                await _settingsService.SetSettingAsync("AllowPartialPayments", AllowPartialPayments);
                await _settingsService.SetSettingAsync("AllowOverpayment", AllowOverpayment);

                _dialogService.ShowMessage("Settings saved successfully. Changes will apply to future actions only.", "Settings Saved");
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error saving settings: {ex.Message}", "Error");
            }
        }
    }
}
