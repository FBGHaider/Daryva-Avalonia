using Daryva.MVVM.Commands;
using Daryva.Services.Api;
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
    private readonly IApiClient _apiClient;
    private bool _isBusy;
    private string _email = string.Empty;
    private string _password = string.Empty;
    private string _errorMessage = string.Empty;
    private string _statusMessage = string.Empty;
    private bool _isTwoFactorScene;
    private string _challengeToken = string.Empty;
    private string _twoFactorCode = string.Empty;

    public SignInViewModel(IAuthService authService, INavigationService navigationService, IServiceProvider serviceProvider, IApiClient apiClient)
    {
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        SignInCommand = new RelayCommand(async _ => await SignInAsync());
        CreateAccountCommand = new RelayCommand(_ => NavigateToCreateAccount());
        ForgotPasswordCommand = new RelayCommand(_ => NavigateToForgotPassword());
        VerifyTwoFactorCommand = new RelayCommand(async _ => await VerifyTwoFactorAsync());
        CancelTwoFactorCommand = new RelayCommand(_ => CancelTwoFactor());

        // Warm the connection to the API host (TLS handshake etc.) the moment this screen appears,
        // well before the user finishes typing and actually submits credentials -- session logs
        // showed the very first request of a session routinely taking 2-4s longer than steady-state
        // ones purely from connection setup. /health carries no credentials, needs no access token
        // (none exists yet at this point), and is explicitly exempt from the API's rate limiter
        // (Daryva.Api/Program.cs), so firing it here is safe and can't affect the real sign-in
        // request or anything else. Best-effort only -- if it fails, the real sign-in request below
        // will simply pay the connection cost itself, same as before this existed.
        _ = WarmApiConnectionAsync();
    }

    private async System.Threading.Tasks.Task WarmApiConnectionAsync()
    {
        try
        {
            // Generous timeout on purpose: this runs fully in the background and blocks nothing
            // the user sees, so there's no cost to giving it a real chance to land even when the
            // API is unusually slow to respond (observed 15s+ response times in testing) -- a
            // timeout that's too short would cancel the request before the underlying connection
            // even finished being established, defeating the point of warming it.
            using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(25));
            await _apiClient.HttpClient.GetAsync("health", cts.Token).ConfigureAwait(false);
        }
        catch
        {
            // Best-effort warm-up; a failure here just means the real sign-in request pays the
            // connection cost itself, exactly as it would have without this.
        }
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
