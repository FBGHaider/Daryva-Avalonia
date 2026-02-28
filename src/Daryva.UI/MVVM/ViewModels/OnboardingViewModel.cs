using System.Collections.ObjectModel;
using System.Windows.Input;
using Daryva.MVVM.Commands;
using Daryva.Services;
using Daryva.Services.Api;
using Daryva.Services.Auth;
using Daryva.Services.Navigation;
using Daryva.Services.OrgContext;

namespace Daryva.MVVM.ViewModels;

public class OnboardingViewModel : BaseViewModel
{
    private readonly IAuthApiService _authApiService;
    private readonly IAuthSessionService _authSessionService;
    private readonly IAuthService _authService;
    private readonly IOrgContext _orgContext;
    private readonly IOrganizationApiService _organizationApiService;
    private readonly IApiClient _apiClient;
    private readonly INavigationService _navigationService;
    private readonly IConfigurationService _configurationService;

    private bool _isBusy;
    private string _errorMessage = string.Empty;
    private bool _isAuthenticated;
    private string _loginEmail = string.Empty;
    private string _loginPassword = string.Empty;
    private string _registerFirstName = string.Empty;
    private string _registerLastName = string.Empty;
    private string _registerEmail = string.Empty;
    private string _registerPassword = string.Empty;
    private string _registerConfirmPassword = string.Empty;
    private string _newOrganizationName = string.Empty;
    private string _joinCode = string.Empty;
    private string _inviteToken = string.Empty;
    private OrganizationDto? _selectedOrganization;
    private bool _keepMeLoggedIn;
    private bool _isLoginScene = true;
    private bool _isVerifyEmailScene;
    private string _verifyEmailAddress = string.Empty;

    public ObservableCollection<OrganizationDto> Organizations { get; } = new();

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        set
        {
            if (SetProperty(ref _errorMessage, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool IsAuthenticated
    {
        get => _isAuthenticated;
        set
        {
            if (SetProperty(ref _isAuthenticated, value))
            {
                OnPropertyChanged(nameof(ShowAuthSection));
                OnPropertyChanged(nameof(ShowOrgSection));
                OnPropertyChanged(nameof(ShowLoginScene));
                OnPropertyChanged(nameof(ShowRegisterScene));
                OnPropertyChanged(nameof(ShowVerifyEmailScene));
            }
        }
    }

    public bool ShowAuthSection => !IsAuthenticated;
    public bool ShowOrgSection => IsAuthenticated;
    public bool ShowLoginScene => ShowAuthSection && IsLoginScene && !IsVerifyEmailScene;
    public bool ShowRegisterScene => ShowAuthSection && !IsLoginScene && !IsVerifyEmailScene;
    public bool ShowVerifyEmailScene => ShowAuthSection && IsVerifyEmailScene;

    public bool IsLoginScene
    {
        get => _isLoginScene;
        set
        {
            if (SetProperty(ref _isLoginScene, value))
            {
                if (IsVerifyEmailScene)
                {
                    _isVerifyEmailScene = false;
                    OnPropertyChanged(nameof(IsVerifyEmailScene));
                }

                OnPropertyChanged(nameof(ShowLoginScene));
                OnPropertyChanged(nameof(ShowRegisterScene));
                OnPropertyChanged(nameof(ShowVerifyEmailScene));
            }
        }
    }

    public bool IsVerifyEmailScene
    {
        get => _isVerifyEmailScene;
        set
        {
            if (SetProperty(ref _isVerifyEmailScene, value))
            {
                OnPropertyChanged(nameof(ShowLoginScene));
                OnPropertyChanged(nameof(ShowRegisterScene));
                OnPropertyChanged(nameof(ShowVerifyEmailScene));
            }
        }
    }

    public string VerifyEmailAddress
    {
        get => _verifyEmailAddress;
        set => SetProperty(ref _verifyEmailAddress, value);
    }

    public bool HasOrganizations => Organizations.Count > 0;

    public string LoginEmail
    {
        get => _loginEmail;
        set => SetProperty(ref _loginEmail, value);
    }

    public string LoginPassword
    {
        get => _loginPassword;
        set => SetProperty(ref _loginPassword, value);
    }

    public string RegisterEmail
    {
        get => _registerEmail;
        set => SetProperty(ref _registerEmail, value);
    }

    public string RegisterFirstName
    {
        get => _registerFirstName;
        set => SetProperty(ref _registerFirstName, value);
    }

    public string RegisterLastName
    {
        get => _registerLastName;
        set => SetProperty(ref _registerLastName, value);
    }

    public string RegisterPassword
    {
        get => _registerPassword;
        set => SetProperty(ref _registerPassword, value);
    }

    public string RegisterConfirmPassword
    {
        get => _registerConfirmPassword;
        set => SetProperty(ref _registerConfirmPassword, value);
    }

    public string NewOrganizationName
    {
        get => _newOrganizationName;
        set => SetProperty(ref _newOrganizationName, value);
    }

    public string JoinCode
    {
        get => _joinCode;
        set => SetProperty(ref _joinCode, value);
    }

    public string InviteToken
    {
        get => _inviteToken;
        set => SetProperty(ref _inviteToken, value);
    }

    public OrganizationDto? SelectedOrganization
    {
        get => _selectedOrganization;
        set => SetProperty(ref _selectedOrganization, value);
    }

    public bool KeepMeLoggedIn
    {
        get => _keepMeLoggedIn;
        set => SetProperty(ref _keepMeLoggedIn, value);
    }

    public ICommand LoginCommand { get; }
    public ICommand RegisterCommand { get; }
    public ICommand RefreshOrganizationsCommand { get; }
    public ICommand UseSelectedOrganizationCommand { get; }
    public ICommand CreateOrganizationCommand { get; }
    public ICommand JoinByCodeCommand { get; }
    public ICommand JoinByInviteCommand { get; }
    public ICommand LogoutCommand { get; }
    public ICommand ShowRegisterSceneCommand { get; }
    public ICommand ShowLoginSceneCommand { get; }
    public ICommand ForgotPasswordCommand { get; }

    public OnboardingViewModel(
        IAuthApiService authApiService,
        IAuthSessionService authSessionService,
        IAuthService authService,
        IOrgContext orgContext,
        IOrganizationApiService organizationApiService,
        IApiClient apiClient,
        INavigationService navigationService,
        IConfigurationService configurationService)
    {
        _authApiService = authApiService;
        _authSessionService = authSessionService;
        _authService = authService;
        _orgContext = orgContext;
        _organizationApiService = organizationApiService;
        _apiClient = apiClient;
        _navigationService = navigationService;
        _configurationService = configurationService;

        LoginCommand = new RelayCommand(async _ => await LoginAsync());
        RegisterCommand = new RelayCommand(async _ => await RegisterAsync());
        RefreshOrganizationsCommand = new RelayCommand(async _ => await LoadOrganizationsAsync());
        UseSelectedOrganizationCommand = new RelayCommand(_ => UseSelectedOrganization());
        CreateOrganizationCommand = new RelayCommand(async _ => await CreateOrganizationAsync());
        JoinByCodeCommand = new RelayCommand(async _ => await JoinByCodeAsync());
        JoinByInviteCommand = new RelayCommand(async _ => await JoinByInviteAsync());
        LogoutCommand = new RelayCommand(async _ => await LogoutAsync());
        ShowRegisterSceneCommand = new RelayCommand(_ => SwitchToRegisterScene());
        ShowLoginSceneCommand = new RelayCommand(_ => SwitchToLoginScene());
        ForgotPasswordCommand = new RelayCommand(async _ => await ForgotPasswordAsync());

        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        var keepLoggedInRaw = _configurationService.GetValue("KeepMeLoggedIn");
        KeepMeLoggedIn = string.Equals(keepLoggedInRaw, "true", StringComparison.OrdinalIgnoreCase);

        IsAuthenticated = _authSessionService.IsAuthenticated;
        if (IsAuthenticated)
        {
            await LoadOrganizationsAsync();
        }
    }

    private async Task LoginAsync()
    {
        await ExecuteBusyAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(LoginEmail) || string.IsNullOrWhiteSpace(LoginPassword))
            {
                ErrorMessage = "Email and password are required.";
                return;
            }

            await _authApiService.LoginAsync(LoginEmail.Trim(), LoginPassword);
            _configurationService.SetLocalValue("KeepMeLoggedIn", KeepMeLoggedIn ? "true" : "false");
            _configurationService.ReloadLocalConfig();
            IsAuthenticated = true;
            LoginPassword = string.Empty;
            await LoadOrganizationsAsync();
        });
    }

    private async Task RegisterAsync()
    {
        await ExecuteBusyAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(RegisterFirstName) ||
                string.IsNullOrWhiteSpace(RegisterLastName) ||
                string.IsNullOrWhiteSpace(RegisterEmail) ||
                string.IsNullOrWhiteSpace(RegisterPassword) ||
                string.IsNullOrWhiteSpace(RegisterConfirmPassword))
            {
                ErrorMessage = "All boxes should be filled.";
                return;
            }

            if (!string.Equals(RegisterPassword, RegisterConfirmPassword, StringComparison.Ordinal))
            {
                ErrorMessage = "Passwords do not match.";
                return;
            }

            var registerResult = await _authApiService.RegisterAsync(
                RegisterEmail.Trim(),
                RegisterPassword,
                RegisterFirstName.Trim(),
                RegisterLastName.Trim());

            VerifyEmailAddress = RegisterEmail.Trim();
            IsAuthenticated = false;
            IsVerifyEmailScene = true;
            ErrorMessage = registerResult.Message;
            RegisterFirstName = string.Empty;
            RegisterLastName = string.Empty;
            RegisterEmail = string.Empty;
            RegisterPassword = string.Empty;
            RegisterConfirmPassword = string.Empty;
        });
    }

    private void SwitchToRegisterScene()
    {
        IsVerifyEmailScene = false;
        IsLoginScene = false;
    }

    private void SwitchToLoginScene()
    {
        IsVerifyEmailScene = false;
        IsLoginScene = true;
    }

    private async Task ForgotPasswordAsync()
    {
        await ExecuteBusyAsync(async () =>
        {
            await Task.CompletedTask;
            ErrorMessage = "Forgot password is not available yet. Please contact support.";
        });
    }

    private async Task LoadOrganizationsAsync()
    {
        await ExecuteBusyAsync(async () =>
        {
            var orgs = await _organizationApiService.GetUserOrganizationsAsync();

            Organizations.Clear();
            foreach (var org in orgs)
            {
                Organizations.Add(org);
            }

            OnPropertyChanged(nameof(HasOrganizations));

            if (_apiClient.CurrentOrgId.HasValue)
            {
                SelectedOrganization = Organizations.FirstOrDefault(o => o.Id == _apiClient.CurrentOrgId.Value);
            }

            SelectedOrganization ??= Organizations.FirstOrDefault();
        });
    }

    private void UseSelectedOrganization()
    {
        if (SelectedOrganization == null)
        {
            ErrorMessage = "Select an organization first.";
            return;
        }

        _apiClient.SetCurrentOrgId(SelectedOrganization.Id);
        _navigationService.NavigateTo<DashboardViewModel>();
    }

    private async Task CreateOrganizationAsync()
    {
        await ExecuteBusyAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(NewOrganizationName))
            {
                ErrorMessage = "Organization name is required.";
                return;
            }

            var created = await _organizationApiService.CreateOrganizationAsync(NewOrganizationName.Trim());
            NewOrganizationName = string.Empty;
            await LoadOrganizationsAsync();
            SelectedOrganization = Organizations.FirstOrDefault(o => o.Id == created.Id) ?? created;
            await _orgContext.SetCurrentOrgAsync(created.Id);
            _navigationService.NavigateTo<DashboardViewModel>();
        });
    }

    private async Task JoinByCodeAsync()
    {
        await ExecuteBusyAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(JoinCode))
            {
                ErrorMessage = "Join code is required.";
                return;
            }

            var result = await _organizationApiService.JoinByCodeAsync(JoinCode.Trim());
            JoinCode = string.Empty;
            _apiClient.SetCurrentOrgId(result.OrganizationId);
            _navigationService.NavigateTo<DashboardViewModel>();
        });
    }

    private async Task JoinByInviteAsync()
    {
        await ExecuteBusyAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(InviteToken))
            {
                ErrorMessage = "Invite token is required.";
                return;
            }

            var result = await _organizationApiService.JoinByInviteAsync(InviteToken.Trim());
            InviteToken = string.Empty;
            await _orgContext.SetCurrentOrgAsync(result.OrganizationId);
            _navigationService.NavigateTo<DashboardViewModel>();
        });
    }

    private async Task LogoutAsync()
    {
        await ExecuteBusyAsync(async () =>
        {
            await _authService.SignOutAsync();
            IsAuthenticated = false;
            Organizations.Clear();
            SelectedOrganization = null;
            OnPropertyChanged(nameof(HasOrganizations));
            // MainViewModel subscribes to IAuthService.StateChanged and navigates to SignInView
        });
    }

    private async Task ExecuteBusyAsync(Func<Task> action)
    {
        if (IsBusy)
            return;

        try
        {
            IsBusy = true;
            ErrorMessage = string.Empty;
            await action();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
