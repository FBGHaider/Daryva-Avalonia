using System.Text.RegularExpressions;
using System.Windows.Input;
using Daryva.MVVM.Commands;
using Daryva.Services;
using Daryva.Services.Api;
using Daryva.Services.Business;
using Daryva.Services.Dialog;

namespace Daryva.MVVM.ViewModels
{
    public class EmailSettingsViewModel : BaseViewModel
    {
        private readonly IConfigurationService _configurationService;
        private readonly IApiClient _apiClient;
        private readonly IEmailSender _emailSender;
        private readonly ISettingsService _settingsService;
        private readonly IDialogService _dialogService;

        private string _smtpServer = string.Empty;
        private int _smtpPort = 587;
        private string _smtpUsername = string.Empty;
        private string _smtpPassword = string.Empty;
        private bool _enableSsl = true;
        private string _fromAddress = string.Empty;
        private string _testEmailAddress = string.Empty;
        private string _emailStatus = "NotConfigured";
        private DateTime? _lastTestTime;

        public EmailSettingsViewModel(
            IConfigurationService configurationService,
            IApiClient apiClient,
            IEmailSender emailSender,
            ISettingsService settingsService,
            IDialogService dialogService)
        {
            _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
            _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
            _emailSender = emailSender ?? throw new ArgumentNullException(nameof(emailSender));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));

            SaveCommand = new RelayCommand(async _ => await SaveAsync());
            TestEmailCommand = new RelayCommand(async _ => await TestEmailAsync(), _ => CanTestEmail());
            ResetCommand = new RelayCommand(async _ => await LoadAsync());

            _ = LoadAsync();
        }

        public ICommand SaveCommand { get; }
        public ICommand TestEmailCommand { get; }
        public ICommand ResetCommand { get; }

        public string SmtpServer
        {
            get => _smtpServer;
            set
            {
                if (SetProperty(ref _smtpServer, value))
                {
                    UpdateEmailStatus();
                    ((RelayCommand)TestEmailCommand).RaiseCanExecuteChanged();
                }
            }
        }

        public int SmtpPort
        {
            get => _smtpPort;
            set
            {
                if (SetProperty(ref _smtpPort, value))
                    ((RelayCommand)TestEmailCommand).RaiseCanExecuteChanged();
            }
        }

        public string SmtpUsername
        {
            get => _smtpUsername;
            set
            {
                if (SetProperty(ref _smtpUsername, value))
                {
                    UpdateEmailStatus();
                    ((RelayCommand)TestEmailCommand).RaiseCanExecuteChanged();
                }
            }
        }

        public string SmtpPassword
        {
            get => _smtpPassword;
            set
            {
                if (SetProperty(ref _smtpPassword, value))
                {
                    UpdateEmailStatus();
                    ((RelayCommand)TestEmailCommand).RaiseCanExecuteChanged();
                }
            }
        }

        public bool EnableSsl
        {
            get => _enableSsl;
            set => SetProperty(ref _enableSsl, value);
        }

        public string FromAddress
        {
            get => _fromAddress;
            set
            {
                if (SetProperty(ref _fromAddress, value))
                    ((RelayCommand)TestEmailCommand).RaiseCanExecuteChanged();
            }
        }

        public string TestEmailAddress
        {
            get => _testEmailAddress;
            set
            {
                if (SetProperty(ref _testEmailAddress, value))
                    ((RelayCommand)TestEmailCommand).RaiseCanExecuteChanged();
            }
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
                var server = _configurationService.GetValue(GetOrgScopedConfigKey("SmtpServer"))?.Trim();
                SmtpServer = server ?? string.Empty;
                var portStr = _configurationService.GetValue(GetOrgScopedConfigKey("SmtpPort"));
                SmtpPort = int.TryParse(portStr, out var port) ? port : 587;
                SmtpUsername = _configurationService.GetValue(GetOrgScopedConfigKey("SmtpUsername")) ?? string.Empty;
                SmtpPassword = _configurationService.GetValue(GetOrgScopedConfigKey("SmtpPassword")) ?? string.Empty;
                var sslStr = _configurationService.GetValue(GetOrgScopedConfigKey("SmtpEnableSsl"));
                EnableSsl = sslStr != null && bool.TryParse(sslStr, out var ssl) && ssl;
                FromAddress = _configurationService.GetValue(GetOrgScopedConfigKey("SmtpFromAddress")) ?? string.Empty;
                TestEmailAddress = await _settingsService.GetSettingAsync(GetOrgScopedSettingKey("EmailTestAddress")) ?? string.Empty;

                EmailStatus = await _settingsService.GetSettingAsync(GetOrgScopedSettingKey("EmailStatus"), "NotConfigured") ?? "NotConfigured";
                var lastTestStr = await _settingsService.GetSettingAsync(GetOrgScopedSettingKey("EmailLastTestTime"));
                if (DateTime.TryParse(lastTestStr, out var lastTest))
                {
                    LastTestTime = lastTest;
                }

                UpdateEmailStatus();
                ((RelayCommand)TestEmailCommand).RaiseCanExecuteChanged();
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error loading settings: {ex.Message}", "Error");
            }
        }

        private bool CanTestEmail()
        {
            if (string.IsNullOrWhiteSpace(SmtpServer) || string.IsNullOrWhiteSpace(SmtpUsername) || string.IsNullOrWhiteSpace(SmtpPassword))
                return false;
            if (SmtpPort < 1 || SmtpPort > 65535)
                return false;
            if (string.IsNullOrWhiteSpace(FromAddress) || string.IsNullOrWhiteSpace(TestEmailAddress))
                return false;
            var trimmed = TestEmailAddress.Trim();
            if (!Regex.IsMatch(trimmed, @"^[^@]+@[^@]+\.[^@]+$", RegexOptions.IgnoreCase))
                return false;
            return true;
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
                _configurationService.SetLocalValue(GetOrgScopedConfigKey("SmtpServer"), SmtpServer);
                _configurationService.SetLocalValue(GetOrgScopedConfigKey("SmtpPort"), SmtpPort.ToString());
                _configurationService.SetLocalValue(GetOrgScopedConfigKey("SmtpUsername"), SmtpUsername);
                if (!string.IsNullOrWhiteSpace(SmtpPassword))
                {
                    _configurationService.SetLocalValue(GetOrgScopedConfigKey("SmtpPassword"), SmtpPassword);
                }
                _configurationService.SetLocalValue(GetOrgScopedConfigKey("SmtpEnableSsl"), EnableSsl.ToString());
                _configurationService.SetLocalValue(GetOrgScopedConfigKey("SmtpFromAddress"), FromAddress);

                // Reload configuration
                _configurationService.ReloadLocalConfig();

                await _settingsService.SetSettingAsync(GetOrgScopedSettingKey("EmailTestAddress"), TestEmailAddress.Trim());

                UpdateEmailStatus();
                await _settingsService.SetSettingAsync(GetOrgScopedSettingKey("EmailStatus"), EmailStatus);
                ((RelayCommand)TestEmailCommand).RaiseCanExecuteChanged();

                _dialogService.ShowMessage(
                    "SMTP settings have been saved successfully!\n\n" +
                    "Ensure 'Test email address' is set, then use 'Test Email' to send a test message.",
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
                var toAddress = TestEmailAddress.Trim();
                if (string.IsNullOrWhiteSpace(toAddress) || !Regex.IsMatch(toAddress, @"^[^@]+@[^@]+\.[^@]+$", RegexOptions.IgnoreCase))
                {
                    _dialogService.ShowMessage("Please enter a valid test email address in the settings above, then save.", "Invalid Test Email");
                    return;
                }

                var success = await _emailSender.SendEmailAsync(
                    toAddress,
                    "Daryva - Test Email",
                    "This is a test email from Daryva. If you received this, your email configuration is working correctly.");

                if (success)
                {
                    EmailStatus = "Connected";
                    LastTestTime = DateTime.UtcNow;
                    await _settingsService.SetSettingAsync(GetOrgScopedSettingKey("EmailStatus"), EmailStatus);
                    await _settingsService.SetSettingAsync(GetOrgScopedSettingKey("EmailLastTestTime"), LastTestTime.Value);
                    _dialogService.ShowMessage($"Test email sent successfully to {toAddress}.\n\nCheck your inbox (and spam folder).", "Success");
                }
                else
                {
                    EmailStatus = "Failed";
                    await _settingsService.SetSettingAsync(GetOrgScopedSettingKey("EmailStatus"), EmailStatus);
                    _dialogService.ShowMessage("Failed to send test email. Please check your SMTP settings.", "Error");
                }
            }
            catch (Exception ex)
            {
                EmailStatus = "Failed";
                await _settingsService.SetSettingAsync(GetOrgScopedSettingKey("EmailStatus"), EmailStatus);
                _dialogService.ShowMessage($"Error sending test email: {ex.Message}", "Error");
            }
        }

        private string GetOrgScopedConfigKey(string key)
        {
            var orgId = _apiClient.CurrentOrgId;
            return orgId.HasValue ? $"Org.{orgId.Value:N}.{key}" : key;
        }

        private string GetOrgScopedSettingKey(string key)
        {
            var orgId = _apiClient.CurrentOrgId;
            return orgId.HasValue ? $"Org.{orgId.Value:N}.{key}" : key;
        }
    }
}
