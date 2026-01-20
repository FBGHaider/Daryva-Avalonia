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

        private bool _rentDueReminderEnabled = true;
        private int _rentDueReminderDaysBefore = 3;
        private bool _rentOverdueReminderEnabled = true;
        private int _rentOverdueReminderDaysAfter = 1;
        private bool _documentExpiryReminderEnabled = true;
        private int _documentExpiryReminderDaysBefore = 30;
        private string _quietHoursStart = "22:00";
        private string _quietHoursEnd = "08:00";
        private bool _sendOnWeekends = true;
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

        public bool RentDueReminderEnabled
        {
            get => _rentDueReminderEnabled;
            set => SetProperty(ref _rentDueReminderEnabled, value);
        }

        public int RentDueReminderDaysBefore
        {
            get => _rentDueReminderDaysBefore;
            set => SetProperty(ref _rentDueReminderDaysBefore, value);
        }

        public bool RentOverdueReminderEnabled
        {
            get => _rentOverdueReminderEnabled;
            set => SetProperty(ref _rentOverdueReminderEnabled, value);
        }

        public int RentOverdueReminderDaysAfter
        {
            get => _rentOverdueReminderDaysAfter;
            set => SetProperty(ref _rentOverdueReminderDaysAfter, value);
        }

        public bool DocumentExpiryReminderEnabled
        {
            get => _documentExpiryReminderEnabled;
            set => SetProperty(ref _documentExpiryReminderEnabled, value);
        }

        public int DocumentExpiryReminderDaysBefore
        {
            get => _documentExpiryReminderDaysBefore;
            set => SetProperty(ref _documentExpiryReminderDaysBefore, value);
        }

        public string QuietHoursStart
        {
            get => _quietHoursStart;
            set => SetProperty(ref _quietHoursStart, value);
        }

        public string QuietHoursEnd
        {
            get => _quietHoursEnd;
            set => SetProperty(ref _quietHoursEnd, value);
        }

        public bool SendOnWeekends
        {
            get => _sendOnWeekends;
            set => SetProperty(ref _sendOnWeekends, value);
        }

        public string DefaultNotificationChannel
        {
            get => _defaultNotificationChannel;
            set => SetProperty(ref _defaultNotificationChannel, value);
        }

        public List<string> NotificationChannelOptions { get; } = new() { "Email", "WhatsApp" };

        private async Task LoadAsync()
        {
            try
            {
                RentDueReminderEnabled = await _settingsService.GetSettingAsync<bool>("RentDueReminderEnabled", true) ?? true;
                RentDueReminderDaysBefore = await _settingsService.GetSettingAsync<int>("RentDueReminderDaysBefore", 3) ?? 3;
                RentOverdueReminderEnabled = await _settingsService.GetSettingAsync<bool>("RentOverdueReminderEnabled", true) ?? true;
                RentOverdueReminderDaysAfter = await _settingsService.GetSettingAsync<int>("RentOverdueReminderDaysAfter", 1) ?? 1;
                DocumentExpiryReminderEnabled = await _settingsService.GetSettingAsync<bool>("DocumentExpiryReminderEnabled", true) ?? true;
                DocumentExpiryReminderDaysBefore = await _settingsService.GetSettingAsync<int>("DocumentExpiryReminderDaysBefore", 30) ?? 30;
                QuietHoursStart = await _settingsService.GetSettingAsync("QuietHoursStart", "22:00") ?? "22:00";
                QuietHoursEnd = await _settingsService.GetSettingAsync("QuietHoursEnd", "08:00") ?? "08:00";
                SendOnWeekends = await _settingsService.GetSettingAsync<bool>("SendOnWeekends", true) ?? true;
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
                await _settingsService.SetSettingAsync("RentDueReminderEnabled", RentDueReminderEnabled);
                await _settingsService.SetSettingAsync("RentDueReminderDaysBefore", RentDueReminderDaysBefore);
                await _settingsService.SetSettingAsync("RentOverdueReminderEnabled", RentOverdueReminderEnabled);
                await _settingsService.SetSettingAsync("RentOverdueReminderDaysAfter", RentOverdueReminderDaysAfter);
                await _settingsService.SetSettingAsync("DocumentExpiryReminderEnabled", DocumentExpiryReminderEnabled);
                await _settingsService.SetSettingAsync("DocumentExpiryReminderDaysBefore", DocumentExpiryReminderDaysBefore);
                await _settingsService.SetSettingAsync("QuietHoursStart", QuietHoursStart);
                await _settingsService.SetSettingAsync("QuietHoursEnd", QuietHoursEnd);
                await _settingsService.SetSettingAsync("SendOnWeekends", SendOnWeekends);
                await _settingsService.SetSettingAsync("DefaultNotificationChannel", DefaultNotificationChannel);

                _dialogService.ShowMessage("Settings saved successfully. Changes will apply to future actions only.", "Settings Saved");
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error saving settings: {ex.Message}", "Error");
            }
        }
    }
}
