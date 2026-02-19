using System.Net.Http;
using System.Windows.Input;
using Daryva.MVVM.Commands;
using Daryva.Services;
using Daryva.Services.Api;
using Daryva.Services.Dialog;

namespace Daryva.MVVM.ViewModels
{
    public class DatabaseSettingsViewModel : BaseViewModel
    {
        private const string ApiBaseUrlKey = "ApiBaseUrl";
        private const string DefaultApiBaseUrl = "http://localhost:5000";

        private readonly IConfigurationService _configurationService;
        private readonly IApiClient _apiClient;
        private readonly IDialogService _dialogService;

        private string _apiBaseUrl = string.Empty;
        private string _connectionStatus = "Not tested";
        private string _currentOrganization = "(Not selected)";

        public DatabaseSettingsViewModel(
            IConfigurationService configurationService,
            IApiClient apiClient,
            IDialogService dialogService)
        {
            _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
            _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));

            SaveApiSettingsCommand = new RelayCommand(async _ => await SaveApiSettingsAsync());
            ResetApiSettingsCommand = new RelayCommand(_ => ResetApiSettings());
            TestApiConnectionCommand = new RelayCommand(async _ => await TestApiConnectionAsync());

            RefreshState();
        }

        public ICommand SaveApiSettingsCommand { get; }
        public ICommand ResetApiSettingsCommand { get; }
        public ICommand TestApiConnectionCommand { get; }

        public string ApiBaseUrl
        {
            get => _apiBaseUrl;
            set => SetProperty(ref _apiBaseUrl, value);
        }

        public string ConnectionStatus
        {
            get => _connectionStatus;
            set => SetProperty(ref _connectionStatus, value);
        }

        public string CurrentOrganization
        {
            get => _currentOrganization;
            set => SetProperty(ref _currentOrganization, value);
        }

        private void RefreshState()
        {
            ApiBaseUrl = NormalizeApiBaseUrl(_configurationService.GetValue(ApiBaseUrlKey) ?? DefaultApiBaseUrl);
            CurrentOrganization = _apiClient.CurrentOrgId?.ToString() ?? "(Not selected)";
        }

        private async Task SaveApiSettingsAsync()
        {
            var normalized = NormalizeApiBaseUrl(ApiBaseUrl);
            if (!IsValidApiUrl(normalized))
            {
                _dialogService.ShowMessage("Please enter a valid API URL (http/https).", "Invalid API URL");
                return;
            }

            _configurationService.SetLocalValue(ApiBaseUrlKey, normalized);
            _configurationService.ReloadLocalConfig();
            ApiBaseUrl = normalized;

            await TestApiConnectionAsync();

            _dialogService.ShowMessage(
                "API URL saved. Restart the app to apply this base URL to all API calls.",
                "API Settings");
        }

        private void ResetApiSettings()
        {
            _configurationService.SetLocalValue(ApiBaseUrlKey, DefaultApiBaseUrl);
            _configurationService.ReloadLocalConfig();
            RefreshState();
            ConnectionStatus = "Not tested";
            _dialogService.ShowMessage(
                "API URL reset to default. Restart the app to apply this base URL to all API calls.",
                "API Settings");
        }

        private async Task TestApiConnectionAsync()
        {
            var normalized = NormalizeApiBaseUrl(ApiBaseUrl);
            if (!IsValidApiUrl(normalized))
            {
                ConnectionStatus = "Invalid URL";
                return;
            }

            try
            {
                using var client = new HttpClient
                {
                    BaseAddress = new Uri(normalized),
                    Timeout = TimeSpan.FromSeconds(5)
                };

                var response = await client.GetAsync("health");
                ConnectionStatus = response.IsSuccessStatusCode
                    ? "Connected"
                    : $"Unreachable ({(int)response.StatusCode})";
            }
            catch (Exception ex)
            {
                ConnectionStatus = $"Unreachable ({ex.Message})";
            }
        }

        private static string NormalizeApiBaseUrl(string value)
        {
            return (value ?? string.Empty).Trim().TrimEnd('/');
        }

        private static bool IsValidApiUrl(string value)
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
            {
                return false;
            }

            return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
        }
    }
}
