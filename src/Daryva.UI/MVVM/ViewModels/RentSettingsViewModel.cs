using System.Windows.Input;
using Daryva.MVVM.Commands;
using Daryva.Services.Business;
using Daryva.Services.Dialog;

namespace Daryva.MVVM.ViewModels
{
    public class RentSettingsViewModel : BaseViewModel
    {
        private readonly ISettingsService _settingsService;
        private readonly IDialogService _dialogService;

        private int _defaultRentDueDay = 1;

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

        private async Task LoadAsync()
        {
            try
            {
                DefaultRentDueDay = await _settingsService.GetSettingAsync<int>("DefaultRentDueDay", 1) ?? 1;
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

                await _settingsService.SetSettingAsync("DefaultRentDueDay", DefaultRentDueDay);

                _dialogService.ShowMessage("Settings saved successfully.", "Settings Saved");
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error saving settings: {ex.Message}", "Error");
            }
        }
    }
}
