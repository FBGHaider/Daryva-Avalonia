using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia.Media.Imaging;
using Daryva.MVVM.Commands;
using Daryva.Services.Api;
using QRCoder;

namespace Daryva.MVVM.ViewModels
{
    /// <summary>
    /// Account page — Security section (password, 2FA, sessions).
    /// </summary>
    public class AccountSecuritySectionViewModel : BaseViewModel
    {
        private readonly IAuthApiService _authApiService;

        private enum SecurityScene { Loading, Overview, Enrolling, ShowRecoveryCodes, ConfirmDisable, ConfirmRegenerate }

        private SecurityScene _scene = SecurityScene.Loading;
        private bool _isBusy;
        private string _errorMessage = string.Empty;
        private bool _isTwoFactorEnabled;
        private string _enrollSecret = string.Empty;
        private Bitmap? _enrollQrCode;
        private string _confirmCode = string.Empty;
        private string _disablePassword = string.Empty;
        private string _regeneratePassword = string.Empty;

        public AccountSecuritySectionViewModel(IAuthApiService authApiService)
        {
            _authApiService = authApiService ?? throw new ArgumentNullException(nameof(authApiService));

            StartEnrollCommand = new RelayCommand(async _ => await StartEnrollAsync());
            ConfirmEnrollCommand = new RelayCommand(async _ => await ConfirmEnrollAsync());
            CancelEnrollCommand = new RelayCommand(_ => ShowOverview());
            DoneWithRecoveryCodesCommand = new RelayCommand(_ => ShowOverview());
            ShowDisableCommand = new RelayCommand(_ => ShowScene(SecurityScene.ConfirmDisable));
            ConfirmDisableCommand = new RelayCommand(async _ => await ConfirmDisableAsync());
            ShowRegenerateCommand = new RelayCommand(_ => ShowScene(SecurityScene.ConfirmRegenerate));
            ConfirmRegenerateCommand = new RelayCommand(async _ => await ConfirmRegenerateAsync());
            CancelPasswordPromptCommand = new RelayCommand(_ => ShowOverview());

            RecoveryCodes = new ObservableCollection<string>();

            _ = LoadStatusAsync();
        }

        public string SectionTitle => "Security";

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

        public bool IsTwoFactorEnabled
        {
            get => _isTwoFactorEnabled;
            private set => SetProperty(ref _isTwoFactorEnabled, value);
        }

        public bool ShowLoadingScene => _scene == SecurityScene.Loading;
        public bool ShowOverviewScene => _scene == SecurityScene.Overview;
        public bool ShowEnrollingScene => _scene == SecurityScene.Enrolling;
        public bool ShowRecoveryCodesScene => _scene == SecurityScene.ShowRecoveryCodes;
        public bool ShowConfirmDisableScene => _scene == SecurityScene.ConfirmDisable;
        public bool ShowConfirmRegenerateScene => _scene == SecurityScene.ConfirmRegenerate;

        public string EnrollSecret
        {
            get => _enrollSecret;
            private set => SetProperty(ref _enrollSecret, value);
        }

        public Bitmap? EnrollQrCode
        {
            get => _enrollQrCode;
            private set => SetProperty(ref _enrollQrCode, value);
        }

        public string ConfirmCode
        {
            get => _confirmCode;
            set => SetProperty(ref _confirmCode, value ?? string.Empty);
        }

        public string DisablePassword
        {
            get => _disablePassword;
            set => SetProperty(ref _disablePassword, value ?? string.Empty);
        }

        public string RegeneratePassword
        {
            get => _regeneratePassword;
            set => SetProperty(ref _regeneratePassword, value ?? string.Empty);
        }

        public ObservableCollection<string> RecoveryCodes { get; }

        public ICommand StartEnrollCommand { get; }
        public ICommand ConfirmEnrollCommand { get; }
        public ICommand CancelEnrollCommand { get; }
        public ICommand DoneWithRecoveryCodesCommand { get; }
        public ICommand ShowDisableCommand { get; }
        public ICommand ConfirmDisableCommand { get; }
        public ICommand ShowRegenerateCommand { get; }
        public ICommand ConfirmRegenerateCommand { get; }
        public ICommand CancelPasswordPromptCommand { get; }

        private void ShowScene(SecurityScene scene)
        {
            ErrorMessage = string.Empty;
            _scene = scene;
            NotifySceneChanged();
        }

        private void ShowOverview()
        {
            ConfirmCode = string.Empty;
            DisablePassword = string.Empty;
            RegeneratePassword = string.Empty;
            ShowScene(SecurityScene.Overview);
        }

        private void NotifySceneChanged()
        {
            OnPropertyChanged(nameof(ShowLoadingScene));
            OnPropertyChanged(nameof(ShowOverviewScene));
            OnPropertyChanged(nameof(ShowEnrollingScene));
            OnPropertyChanged(nameof(ShowRecoveryCodesScene));
            OnPropertyChanged(nameof(ShowConfirmDisableScene));
            OnPropertyChanged(nameof(ShowConfirmRegenerateScene));
        }

        private async Task LoadStatusAsync()
        {
            try
            {
                var me = await _authApiService.GetMeAsync().ConfigureAwait(true);
                IsTwoFactorEnabled = me?.TwoFactorEnabled ?? false;
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Could not load security status: {ex.Message}";
            }
            finally
            {
                ShowScene(SecurityScene.Overview);
            }
        }

        private async Task StartEnrollAsync()
        {
            if (IsBusy)
                return;

            IsBusy = true;
            ErrorMessage = string.Empty;
            try
            {
                var result = await _authApiService.EnrollTwoFactorAsync().ConfigureAwait(true);
                EnrollSecret = result.Secret;
                EnrollQrCode = BuildQrCodeBitmap(result.OtpAuthUri);
                ConfirmCode = string.Empty;
                ShowScene(SecurityScene.Enrolling);
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

        private async Task ConfirmEnrollAsync()
        {
            if (IsBusy)
                return;

            if (string.IsNullOrWhiteSpace(ConfirmCode))
            {
                ErrorMessage = "Enter the 6-digit code from your authenticator app.";
                return;
            }

            IsBusy = true;
            ErrorMessage = string.Empty;
            try
            {
                var result = await _authApiService.ConfirmTwoFactorAsync(ConfirmCode.Trim()).ConfigureAwait(true);
                if (!result.Success)
                {
                    ErrorMessage = result.Message;
                    return;
                }

                IsTwoFactorEnabled = true;
                ReplaceRecoveryCodes(result.RecoveryCodes);
                ConfirmCode = string.Empty;
                ShowScene(SecurityScene.ShowRecoveryCodes);
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

        private async Task ConfirmDisableAsync()
        {
            if (IsBusy)
                return;

            if (string.IsNullOrWhiteSpace(DisablePassword))
            {
                ErrorMessage = "Enter your password to confirm.";
                return;
            }

            IsBusy = true;
            ErrorMessage = string.Empty;
            try
            {
                var result = await _authApiService.DisableTwoFactorAsync(DisablePassword).ConfigureAwait(true);
                if (!result.Success)
                {
                    ErrorMessage = result.Message;
                    return;
                }

                IsTwoFactorEnabled = false;
                ShowOverview();
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

        private async Task ConfirmRegenerateAsync()
        {
            if (IsBusy)
                return;

            if (string.IsNullOrWhiteSpace(RegeneratePassword))
            {
                ErrorMessage = "Enter your password to confirm.";
                return;
            }

            IsBusy = true;
            ErrorMessage = string.Empty;
            try
            {
                var result = await _authApiService.RegenerateRecoveryCodesAsync(RegeneratePassword).ConfigureAwait(true);
                if (!result.Success)
                {
                    ErrorMessage = result.Message;
                    return;
                }

                ReplaceRecoveryCodes(result.RecoveryCodes);
                RegeneratePassword = string.Empty;
                ShowScene(SecurityScene.ShowRecoveryCodes);
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

        private void ReplaceRecoveryCodes(IEnumerable<string> codes)
        {
            RecoveryCodes.Clear();
            foreach (var code in codes)
                RecoveryCodes.Add(code);
        }

        private static Bitmap BuildQrCodeBitmap(string otpAuthUri)
        {
            using var qrGenerator = new QRCodeGenerator();
            using var qrData = qrGenerator.CreateQrCode(otpAuthUri, QRCodeGenerator.ECCLevel.Q);
            var pngQrCode = new PngByteQRCode(qrData);
            var bytes = pngQrCode.GetGraphic(8);
            using var stream = new MemoryStream(bytes);
            return new Bitmap(stream);
        }
    }
}
