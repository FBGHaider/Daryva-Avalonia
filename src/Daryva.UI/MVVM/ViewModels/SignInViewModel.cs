using Daryva.MVVM.Commands;
using Daryva.Services.Auth;
using Daryva.Services.Navigation;
using Microsoft.Extensions.DependencyInjection;

namespace Daryva.MVVM.ViewModels;

/// <summary>
/// Auth gate: sign in with email/password against Daryva.Api. Shown when user is not signed in.
/// On success, MainViewModel reacts to IAuthService.StateChanged and navigates onward (org
/// selection or dashboard) -- this view does not navigate itself.
/// </summary>
public class SignInViewModel : BaseViewModel
{
    private readonly IAuthService _authService;
    private readonly INavigationService _navigationService;
    private readonly IServiceProvider _serviceProvider;
    private bool _isBusy;
    private string _email = string.Empty;
    private string _password = string.Empty;
    private string _errorMessage = string.Empty;
    private string _statusMessage = string.Empty;
    private bool _isTwoFactorScene;
    private string _challengeToken = string.Empty;
    private string _twoFactorCode = string.Empty;

    public SignInViewModel(IAuthService authService, INavigationService navigationService, IServiceProvider serviceProvider)
    {
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        SignInCommand = new RelayCommand(async _ => await SignInAsync());
        CreateAccountCommand = new RelayCommand(_ => NavigateToCreateAccount());
        ForgotPasswordCommand = new RelayCommand(_ => NavigateToForgotPassword());
        VerifyTwoFactorCommand = new RelayCommand(async _ => await VerifyTwoFactorAsync());
        CancelTwoFactorCommand = new RelayCommand(_ => CancelTwoFactor());
    }

    private void NavigateToCreateAccount()
    {
        // Resolved on demand (not constructor-injected) so OnboardingViewModel isn't built just to show
        // SignInView; it's only needed if the user actually clicks "Create account".
        var onboarding = _serviceProvider.GetRequiredService<OnboardingViewModel>();
        onboarding.IsLoginScene = false;
        _navigationService.NavigateTo(onboarding);
    }

    private void NavigateToForgotPassword()
    {
        var onboarding = _serviceProvider.GetRequiredService<OnboardingViewModel>();
        onboarding.ForgotPasswordEmail = Email;
        onboarding.IsForgotPasswordScene = true;
        _navigationService.NavigateTo(onboarding);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public string Email
    {
        get => _email;
        set => SetProperty(ref _email, value);
    }

    public string Password
    {
        get => _password;
        set => SetProperty(ref _password, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        set
        {
            if (SetProperty(ref _errorMessage, value ?? string.Empty))
                OnPropertyChanged(nameof(HasErrorMessage));
        }
    }

    public bool HasErrorMessage => !string.IsNullOrWhiteSpace(ErrorMessage);

    /// <summary>Neutral confirmation text (e.g. "Password reset -- please sign in"), distinct from ErrorMessage's red styling.</summary>
    public string StatusMessage
    {
        get => _statusMessage;
        set
        {
            if (SetProperty(ref _statusMessage, value ?? string.Empty))
                OnPropertyChanged(nameof(HasStatusMessage));
        }
    }

    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

    /// <summary>True once LoginAsync has come back with RequiresTwoFactor -- shows the code-entry panel instead of email/password.</summary>
    public bool IsTwoFactorScene
    {
        get => _isTwoFactorScene;
        set => SetProperty(ref _isTwoFactorScene, value);
    }

    /// <summary>Either a 6-digit authenticator code or a one-time recovery code -- the backend accepts both on the same field.</summary>
    public string TwoFactorCode
    {
        get => _twoFactorCode;
        set => SetProperty(ref _twoFactorCode, value);
    }

    public RelayCommand SignInCommand { get; }
    public RelayCommand CreateAccountCommand { get; }
    public RelayCommand ForgotPasswordCommand { get; }
    public RelayCommand VerifyTwoFactorCommand { get; }
    public RelayCommand CancelTwoFactorCommand { get; }

    private async System.Threading.Tasks.Task SignInAsync()
    {
        if (IsBusy)
            return;
        IsBusy = true;
        ErrorMessage = string.Empty;
        StatusMessage = string.Empty;
        try
        {
            if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
            {
                ErrorMessage = "Email and password are required.";
                return;
            }

            var result = await _authService.SignInAsync(Email.Trim(), Password).ConfigureAwait(true);
            if (result.RequiresTwoFactor)
            {
                _challengeToken = result.ChallengeToken ?? string.Empty;
                Password = string.Empty;
                IsTwoFactorScene = true;
                return;
            }

            Password = string.Empty;
            // MainViewModel subscribes to IAuthService.StateChanged and navigates onward from here.
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

    private async System.Threading.Tasks.Task VerifyTwoFactorAsync()
    {
        if (IsBusy)
            return;
        IsBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            if (string.IsNullOrWhiteSpace(TwoFactorCode))
            {
                ErrorMessage = "Enter the code from your authenticator app, or a recovery code.";
                return;
            }

            await _authService.VerifyTwoFactorAsync(_challengeToken, TwoFactorCode.Trim()).ConfigureAwait(true);
            TwoFactorCode = string.Empty;
            // MainViewModel subscribes to IAuthService.StateChanged and navigates onward from here.
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

    private void CancelTwoFactor()
    {
        _challengeToken = string.Empty;
        TwoFactorCode = string.Empty;
        ErrorMessage = string.Empty;
        IsTwoFactorScene = false;
    }
}
