using System.Windows.Input;
using Daryva.MVVM.Commands;
using Daryva.MVVM.Models;
using Daryva.Services.Business;
using Daryva.Services.Dialog;

namespace Daryva.MVVM.ViewModels
{
    /// <summary>
    /// Account page — Notifications section (personal notification preferences).
    /// </summary>
    public class AccountNotificationsSectionViewModel : BaseViewModel
    {
        private readonly IUserPreferencesService _prefsService;
        private readonly IDialogService _dialogService;

        private bool _rentAlertsEnabled = true;
        private bool _docsExpiryEnabled = true;
        private bool _teamActivityEnabled = true;
        private bool _productUpdatesEnabled = true;
        private bool _inAppEnabled = true;
        private bool _emailEnabled = false;

        public AccountNotificationsSectionViewModel(IUserPreferencesService prefsService, IDialogService dialogService)
        {
            _prefsService = prefsService ?? throw new ArgumentNullException(nameof(prefsService));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));

            SaveNotificationPrefsCommand = new RelayCommand(async _ => await SaveAsync());
            ResetNotificationPrefsCommand = new RelayCommand(async _ => await LoadAsync());

            _ = LoadAsync();
        }

        public string SectionTitle => "Notifications";

        public bool RentAlertsEnabled
        {
            get => _rentAlertsEnabled;
            set => SetProperty(ref _rentAlertsEnabled, value);
        }

        public bool DocsExpiryEnabled
        {
            get => _docsExpiryEnabled;
            set => SetProperty(ref _docsExpiryEnabled, value);
        }

        public bool TeamActivityEnabled
        {
            get => _teamActivityEnabled;
            set => SetProperty(ref _teamActivityEnabled, value);
        }

        public bool ProductUpdatesEnabled
        {
            get => _productUpdatesEnabled;
            set => SetProperty(ref _productUpdatesEnabled, value);
        }

        public bool InAppEnabled
        {
            get => _inAppEnabled;
            set => SetProperty(ref _inAppEnabled, value);
        }

        public bool EmailEnabled
        {
            get => _emailEnabled;
            set => SetProperty(ref _emailEnabled, value);
        }

        public ICommand SaveNotificationPrefsCommand { get; }
        public ICommand ResetNotificationPrefsCommand { get; }

        private async Task LoadAsync()
        {
            try
            {
                var prefs = await _prefsService.GetNotificationPreferencesAsync().ConfigureAwait(false);
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    RentAlertsEnabled = prefs.RentAlertsEnabled;
                    DocsExpiryEnabled = prefs.DocsExpiryEnabled;
                    TeamActivityEnabled = prefs.TeamActivityEnabled;
                    ProductUpdatesEnabled = prefs.ProductUpdatesEnabled;
                    InAppEnabled = prefs.InAppEnabled;
                    EmailEnabled = prefs.EmailEnabled;
                });
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Could not load preferences: {ex.Message}", "Error");
            }
        }

        private async Task SaveAsync()
        {
            try
            {
                var prefs = new UserNotificationPreferences
                {
                    RentAlertsEnabled = RentAlertsEnabled,
                    DocsExpiryEnabled = DocsExpiryEnabled,
                    TeamActivityEnabled = TeamActivityEnabled,
                    ProductUpdatesEnabled = ProductUpdatesEnabled,
                    InAppEnabled = InAppEnabled,
                    EmailEnabled = EmailEnabled
                };
                await _prefsService.UpdateNotificationPreferencesAsync(prefs).ConfigureAwait(false);
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    _dialogService.ShowMessage("Notification preferences saved.", "Saved"));
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Could not save preferences: {ex.Message}", "Error");
            }
        }
    }
}
