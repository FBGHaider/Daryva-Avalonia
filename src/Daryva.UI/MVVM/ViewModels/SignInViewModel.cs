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

    public SignInViewModel(IAuthService authService, INavigationService navigationService, IServiceProvider serviceProvider)
    {
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        SignInCommand = new RelayCommand(async _ => await SignInAsync());
        CreateAccountCommand = new RelayCommand(_ => NavigateToCreateAccount());
        ForgotPasswordCommand = new RelayCommand(_ => NavigateToForgotPassword());
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

    public RelayCommand SignInCommand { get; }
    public RelayCommand CreateAccountCommand { get; }
    public RelayCommand ForgotPasswordCommand { get; }

    private async System.Threading.Tasks.Task SignInAsync()
    {
        if (IsBusy)
            return;
        IsBusy = true;
        ErrorMessage = string.Empty;
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
                ErrorMessage = "This account has two-factor authentication enabled, which isn't supported in this app version yet. Please contact support.";
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
}
