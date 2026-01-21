using System.Windows.Input;
using Daryva.MVVM.Commands;
using Daryva.Services;
using Daryva.Services.Business;
using Daryva.Services.Dialog;

namespace Daryva.MVVM.ViewModels
{
    public class EmailSettingsViewModel : BaseViewModel
    {
        private readonly IConfigurationService _configurationService;
        private readonly IEmailSender _emailSender;
        private readonly ISettingsService _settingsService;
        private readonly IDialogService _dialogService;

        private string _smtpServer = string.Empty;
        private int _smtpPort = 587;
        private string _smtpUsername = string.Empty;
        private string _smtpPassword = string.Empty;
        private bool _enableSsl = true;
        private string _fromAddress = string.Empty;
        private string _emailStatus = "NotConfigured";
        private DateTime? _lastTestTime;

        public EmailSettingsViewModel(
            IConfigurationService configurationService,
            IEmailSender emailSender,
            ISettingsService settingsService,
            IDialogService dialogService)
        {
            _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
            _emailSender = emailSender ?? throw new ArgumentNullException(nameof(emailSender));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));

            SaveCommand = new RelayCommand(async _ => await SaveAsync());
            TestEmailCommand = new RelayCommand(async _ => await TestEmailAsync());
            ResetCommand = new RelayCommand(async _ => await LoadAsync());

            _ = LoadAsync();
        }

        public ICommand SaveCommand { get; }
        public ICommand TestEmailCommand { get; }
        public ICommand ResetCommand { get; }

        public string SmtpServer
        {
            get => _smtpServer;
            set => SetProperty(ref _smtpServer, value);
        }

        public int SmtpPort
        {
            get => _smtpPort;
            set => SetProperty(ref _smtpPort, value);
        }

        public string SmtpUsername
        {
            get => _smtpUsername;
            set => SetProperty(ref _smtpUsername, value);
        }

        public string SmtpPassword
        {
            get => _smtpPassword;
            set => SetProperty(ref _smtpPassword, value);
        }

        public bool EnableSsl
        {
            get => _enableSsl;
            set => SetProperty(ref _enableSsl, value);
        }

        public string FromAddress
        {
            get => _fromAddress;
            set => SetProperty(ref _fromAddress, value);
        }

        public string EmailStatus
        {
            get => _emailStatus;
            set => SetProperty(ref _emailStatus, value);
        }

        public DateTime? LastTestTime
        {
            get => _lastTestTime;
            set => SetProperty(ref _lastTestTime, value);
        }

        private async Task LoadAsync()
        {
            try
            {
                SmtpServer = _configurationService.GetValue("SmtpServer") ?? string.Empty;
                var portStr = _configurationService.GetValue("SmtpPort");
                SmtpPort = int.TryParse(portStr, out var port) ? port : 587;
                SmtpUsername = _configurationService.GetValue("SmtpUsername") ?? string.Empty;
                SmtpPassword = _configurationService.GetValue("SmtpPassword") ?? string.Empty;
                var sslStr = _configurationService.GetValue("SmtpEnableSsl");
                EnableSsl = sslStr != null && bool.TryParse(sslStr, out var ssl) && ssl;
                FromAddress = _configurationService.GetValue("SmtpFromAddress") ?? string.Empty;

                EmailStatus = await _settingsService.GetSettingAsync("EmailStatus", "NotConfigured") ?? "NotConfigured";
                var lastTestStr = await _settingsService.GetSettingAsync("EmailLastTestTime");
                if (DateTime.TryParse(lastTestStr, out var lastTest))
                {
                    LastTestTime = lastTest;
                }

                UpdateEmailStatus();
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error loading settings: {ex.Message}", "Error");
            }
        }

        private void UpdateEmailStatus()
        {
            if (string.IsNullOrWhiteSpace(SmtpServer) || string.IsNullOrWhiteSpace(SmtpUsername) || string.IsNullOrWhiteSpace(SmtpPassword))
            {
                EmailStatus = "NotConfigured";
            }
            else
            {
                EmailStatus = "Configured";
            }
        }

        private async Task SaveAsync()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(SmtpServer))
                {
                    _dialogService.ShowMessage("SMTP Server is required.", "Validation Error");
                    return;
                }

                if (SmtpPort < 1 || SmtpPort > 65535)
                {
                    _dialogService.ShowMessage("SMTP Port must be between 1 and 65535.", "Validation Error");
                    return;
                }

                if (string.IsNullOrWhiteSpace(SmtpUsername))
                {
                    _dialogService.ShowMessage("SMTP Username is required.", "Validation Error");
                    return;
                }

                if (string.IsNullOrWhiteSpace(FromAddress))
                {
                    _dialogService.ShowMessage("From Address is required.", "Validation Error");
                    return;
                }

                // Save SMTP settings to App.config.local
                _configurationService.SetLocalValue("SmtpServer", SmtpServer);
                _configurationService.SetLocalValue("SmtpPort", SmtpPort.ToString());
                _configurationService.SetLocalValue("SmtpUsername", SmtpUsername);
                if (!string.IsNullOrWhiteSpace(SmtpPassword))
                {
                    _configurationService.SetLocalValue("SmtpPassword", SmtpPassword);
                }
                _configurationService.SetLocalValue("SmtpEnableSsl", EnableSsl.ToString());
                _configurationService.SetLocalValue("SmtpFromAddress", FromAddress);

                // Reload configuration
                _configurationService.ReloadLocalConfig();

                UpdateEmailStatus();
                await _settingsService.SetSettingAsync("EmailStatus", EmailStatus);
                
                _dialogService.ShowMessage(
                    "SMTP settings have been saved successfully!\n\n" +
                    "You can now test the email configuration using the 'Test Email' button.",
                    "Settings Saved");
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error saving settings: {ex.Message}", "Error");
            }
        }

        private async Task TestEmailAsync()
        {
            try
            {
                var testEmail = _dialogService.ShowInputDialog(
                    "Enter your email address to send a test email:",
                    "Test Email",
                    SmtpUsername);

                if (string.IsNullOrWhiteSpace(testEmail))
                    return;

                var success = await _emailSender.SendEmailAsync(
                    testEmail,
                    "Daryva - Test Email",
                    "This is a test email from Daryva. If you received this, your email configuration is working correctly.");

                if (success)
                {
                    EmailStatus = "Connected";
                    LastTestTime = DateTime.UtcNow;
                    await _settingsService.SetSettingAsync("EmailStatus", EmailStatus);
                    await _settingsService.SetSettingAsync("EmailLastTestTime", LastTestTime.Value);
                    _dialogService.ShowMessage("Test email sent successfully!", "Success");
                }
                else
                {
                    EmailStatus = "Failed";
                    await _settingsService.SetSettingAsync("EmailStatus", EmailStatus);
                    _dialogService.ShowMessage("Failed to send test email. Please check your SMTP settings.", "Error");
                }
            }
            catch (Exception ex)
            {
                EmailStatus = "Failed";
                await _settingsService.SetSettingAsync("EmailStatus", EmailStatus);
                _dialogService.ShowMessage($"Error sending test email: {ex.Message}", "Error");
            }
        }
    }
}
