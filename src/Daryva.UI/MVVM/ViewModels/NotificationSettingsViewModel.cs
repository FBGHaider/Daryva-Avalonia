using System.Windows.Input;
using Daryva.MVVM.Commands;
using Daryva.Services.Business;
using Daryva.Services.Dialog;

namespace Daryva.MVVM.ViewModels
{
    public class NotificationSettingsViewModel : BaseViewModel
    {
        private readonly ISettingsService _settingsService;
        private readonly IDialogService _dialogService;

        private string _defaultNotificationChannel = "Email";

        public NotificationSettingsViewModel(ISettingsService settingsService, IDialogService dialogService)
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));

            SaveCommand = new RelayCommand(async _ => await SaveAsync());
            ResetCommand = new RelayCommand(async _ => await LoadAsync());

            _ = LoadAsync();
        }

        public ICommand SaveCommand { get; }
        public ICommand ResetCommand { get; }

        public string DefaultNotificationChannel
        {
            get => _defaultNotificationChannel;
            set => SetProperty(ref _defaultNotificationChannel, value);
        }

        public List<string> NotificationChannelOptions { get; } = new() { "Email", "SMS", "WhatsApp" };

        private async Task LoadAsync()
        {
            try
            {
                DefaultNotificationChannel = await _settingsService.GetSettingAsync("DefaultNotificationChannel", "Email") ?? "Email";
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
                await _settingsService.SetSettingAsync("DefaultNotificationChannel", DefaultNotificationChannel);

                _dialogService.ShowMessage("Settings saved successfully.", "Settings Saved");
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error saving settings: {ex.Message}", "Error");
            }
        }
    }
}
