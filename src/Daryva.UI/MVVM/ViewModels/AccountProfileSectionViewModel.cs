using System.Collections.ObjectModel;
using System.Windows.Input;
using Daryva.MVVM.Commands;
using Daryva.MVVM.Models;
using Daryva.Services.Auth;
using Daryva.Services.Business;
using Daryva.Services.Dialog;

namespace Daryva.MVVM.ViewModels
{
    /// <summary>
    /// Account page — Profile section (name, email, phone, time zone, avatar).
    /// </summary>
    public class AccountProfileSectionViewModel : BaseViewModel
    {
        private readonly IUserProfileService _profileService;
        private readonly IDialogService _dialogService;
        private readonly IAuthService _authService;

        private string _fullName = string.Empty;
        private string _email = string.Empty;
        private string? _phone;
        private string _timeZoneId = "GMT Standard Time";
        private string _emailHelperText = "Email is managed by your sign-in provider.";

        public AccountProfileSectionViewModel(IUserProfileService profileService, IDialogService dialogService, IAuthService authService)
        {
            _profileService = profileService ?? throw new ArgumentNullException(nameof(profileService));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));

            SaveProfileCommand = new RelayCommand(async _ => await SaveProfileAsync(), _ => CanSaveProfile());
            ResetProfileCommand = new RelayCommand(async _ => await LoadProfileAsync());
            SignOutCommand = new RelayCommand(async _ => await _authService.SignOutAsync().ConfigureAwait(true));

            TimeZoneIds = new ObservableCollection<string>(
                TimeZoneInfo.GetSystemTimeZones().Select(tz => tz.Id).OrderBy(id => id).ToList());

            _ = LoadProfileAsync();
        }

        public string SectionTitle => "Profile";

        public string FullName
        {
            get => _fullName;
            set => SetProperty(ref _fullName, value ?? string.Empty);
        }

        public string Email
        {
            get => _email;
            set => SetProperty(ref _email, value ?? string.Empty);
        }

        public string? Phone
        {
            get => _phone;
            set => SetProperty(ref _phone, value);
        }

        public string TimeZoneId
        {
            get => _timeZoneId;
            set => SetProperty(ref _timeZoneId, value ?? "GMT Standard Time");
        }

        public string EmailHelperText
        {
            get => _emailHelperText;
            set => SetProperty(ref _emailHelperText, value ?? string.Empty);
        }

        public ObservableCollection<string> TimeZoneIds { get; }

        public ICommand SaveProfileCommand { get; }
        public ICommand ResetProfileCommand { get; }
        public ICommand SignOutCommand { get; }

        private bool CanSaveProfile()
        {
            return !string.IsNullOrWhiteSpace(FullName);
        }

        private async Task LoadProfileAsync()
        {
            try
            {
                var profile = await _profileService.GetProfileAsync().ConfigureAwait(false);
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    FullName = profile.FullName;
                    Email = profile.Email;
                    Phone = profile.Phone;
                    TimeZoneId = string.IsNullOrEmpty(profile.TimeZoneId) ? "GMT Standard Time" : profile.TimeZoneId;
                });
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Could not load profile: {ex.Message}", "Error");
            }
        }

        private async Task SaveProfileAsync()
        {
            if (string.IsNullOrWhiteSpace(FullName))
            {
                _dialogService.ShowMessage("Full name is required.", "Validation");
                return;
            }

            try
            {
                var profile = await _profileService.GetProfileAsync().ConfigureAwait(false);
                profile.FullName = FullName.Trim();
                profile.Phone = string.IsNullOrWhiteSpace(Phone) ? null : Phone.Trim();
                profile.TimeZoneId = TimeZoneId ?? "GMT Standard Time";
                await _profileService.UpdateProfileAsync(profile).ConfigureAwait(false);
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    _dialogService.ShowMessage("Profile saved successfully.", "Saved"));
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Could not save profile: {ex.Message}", "Error");
            }
        }
    }
}
