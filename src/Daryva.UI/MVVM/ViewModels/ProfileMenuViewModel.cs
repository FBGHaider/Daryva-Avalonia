using System.Windows.Input;
using Daryva.MVVM.Commands;
using Daryva.Services.Api;
using Daryva.Services.Navigation;

namespace Daryva.MVVM.ViewModels
{
    /// <summary>
    /// ViewModel for the header profile avatar and dropdown menu.
    /// </summary>
    public class ProfileMenuViewModel : BaseViewModel
    {
        private readonly INavigationService _navigationService;
        private readonly IAuthApiService _authApiService;
        private readonly IAuthSessionService _authSessionService;

        private bool _isOpen;
        private string _userDisplayName = "User";
        private string _userInitials = "U";
        private string? _orgName;

        public ProfileMenuViewModel(
            INavigationService navigationService,
            IAuthApiService authApiService,
            IAuthSessionService authSessionService)
        {
            _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
            _authApiService = authApiService ?? throw new ArgumentNullException(nameof(authApiService));
            _authSessionService = authSessionService ?? throw new ArgumentNullException(nameof(authSessionService));

            ToggleOpenCommand = new RelayCommand(_ => ToggleOpen());
            CloseCommand = new RelayCommand(_ => Close());
            OpenMyProfileCommand = new RelayCommand(_ => OpenMyProfile());
            OpenOrganisationCommand = new RelayCommand(_ => OpenOrganisation());
            OpenPreferencesCommand = new RelayCommand(_ => OpenPreferences());
            OpenSupportCommand = new RelayCommand(_ => OpenSupport());
            LogoutCommand = new RelayCommand(async _ => await LogoutAsync());

            _ = LoadUserInfoAsync();
        }

        public bool IsOpen
        {
            get => _isOpen;
            set => SetProperty(ref _isOpen, value);
        }

        public string UserDisplayName
        {
            get => _userDisplayName;
            private set => SetProperty(ref _userDisplayName, value);
        }

        public string UserInitials
        {
            get => _userInitials;
            private set => SetProperty(ref _userInitials, value);
        }

        /// <summary>Current organisation name (optional).</summary>
        public string? OrgName
        {
            get => _orgName;
            private set
            {
                if (SetProperty(ref _orgName, value))
                    OnPropertyChanged(nameof(HasOrgName));
            }
        }

        /// <summary>True when org name is shown in menu header.</summary>
        public bool HasOrgName => !string.IsNullOrWhiteSpace(OrgName);

        public ICommand ToggleOpenCommand { get; }
        public ICommand CloseCommand { get; }
        public ICommand OpenMyProfileCommand { get; }
        public ICommand OpenOrganisationCommand { get; }
        public ICommand OpenPreferencesCommand { get; }
        public ICommand OpenSupportCommand { get; }
        public ICommand LogoutCommand { get; }

        private void ToggleOpen()
        {
            IsOpen = !IsOpen;
        }

        public void Close()
        {
            IsOpen = false;
        }

        private async Task LoadUserInfoAsync()
        {
            try
            {
                var me = await _authApiService.GetMeAsync();
                if (me != null)
                {
                    var first = (me.FirstName ?? "").Trim();
                    var last = (me.LastName ?? "").Trim();
                    UserDisplayName = string.IsNullOrWhiteSpace(first) && string.IsNullOrWhiteSpace(last)
                        ? me.Email ?? "User"
                        : $"{first} {last}".Trim();
                    UserInitials = GetInitials(first, last, me.Email);
                }
                else
                {
                    UserDisplayName = _authSessionService.Email ?? "User";
                    UserInitials = GetInitialsFromEmail(_authSessionService.Email);
                }

                // Optional: load current org name if API exposes it
                // OrgName = ...
            }
            catch
            {
                UserDisplayName = _authSessionService.Email ?? "User";
                UserInitials = GetInitialsFromEmail(_authSessionService.Email);
            }
        }

        private static string GetInitials(string first, string last, string? email)
        {
            if (!string.IsNullOrWhiteSpace(first) && !string.IsNullOrWhiteSpace(last))
                return $"{char.ToUpperInvariant(first[0])}{char.ToUpperInvariant(last[0])}";
            if (!string.IsNullOrWhiteSpace(first))
                return first.Length >= 2 ? first[..2].ToUpperInvariant() : first.ToUpperInvariant();
            return GetInitialsFromEmail(email);
        }

        private static string GetInitialsFromEmail(string? email)
        {
            if (string.IsNullOrWhiteSpace(email)) return "U";
            var local = email.Split('@', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (string.IsNullOrWhiteSpace(local) || local.Length < 2) return "U";
            return local[..2].ToUpperInvariant();
        }

        private void OpenMyProfile()
        {
            Close();
            _navigationService.NavigateTo<AccountViewModel>();
        }

        private void OpenOrganisation()
        {
            Close();
            _navigationService.NavigateTo<OrganisationViewModel>();
        }

        private void OpenPreferences()
        {
            Close();
            _navigationService.NavigateTo<SettingsViewModel>();
        }

        private void OpenSupport()
        {
            Close();
            // Help & Support — could open URL or a support view
            _navigationService.NavigateTo<SettingsViewModel>();
        }

        private async Task LogoutAsync()
        {
            Close();
            await _authApiService.LogoutAsync();
            _navigationService.NavigateTo<OnboardingViewModel>();
        }
    }
}
