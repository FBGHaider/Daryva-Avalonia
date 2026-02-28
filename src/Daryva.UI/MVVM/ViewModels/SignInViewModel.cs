using Avalonia.Threading;
using Daryva.MVVM.Commands;
using Daryva.Services.Auth;
using Daryva.Services.Dialog;
using Daryva.Services.Navigation;

namespace Daryva.MVVM.ViewModels;

/// <summary>
/// Auth gate: sign in via OIDC. Shown when user is not signed in.
/// </summary>
public class SignInViewModel : BaseViewModel
{
    private readonly IAuthService _authService;
    private readonly INavigationService _navigationService;
    private readonly IDialogService _dialogService;
    private bool _isBusy;
    private string _errorMessage = string.Empty;

    public SignInViewModel(IAuthService authService, INavigationService navigationService, IDialogService dialogService)
    {
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        SignInCommand = new RelayCommand(async _ => await SignInAsync());
    }

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
            if (SetProperty(ref _errorMessage, value ?? string.Empty))
                OnPropertyChanged(nameof(HasErrorMessage));
        }
    }

    public bool HasErrorMessage => !string.IsNullOrWhiteSpace(ErrorMessage);

    public RelayCommand SignInCommand { get; }

    private async System.Threading.Tasks.Task SignInAsync()
    {
        if (IsBusy)
            return;
        IsBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            await _authService.SignInAsync().ConfigureAwait(true);
            // Ensure we're on UI thread and leave sign-in screen; MainViewModel may also run post-sign-in logic via StateChanged.
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _navigationService.NavigateTo<SetupRequiredViewModel>();
            });
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            _dialogService.ShowMessage($"Sign-in failed: {ex.Message}", "Sign-in error");
        }
        finally
        {
            IsBusy = false;
        }
    }
}
