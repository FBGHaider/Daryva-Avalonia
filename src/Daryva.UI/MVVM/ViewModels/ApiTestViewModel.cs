using System.Collections.ObjectModel;
using System.Windows.Input;
using Daryva.MVVM.Commands;
using Daryva.Services;
using Daryva.Services.Api;
using Daryva.Services.Dialog;

namespace Daryva.MVVM.ViewModels;

/// <summary>
/// ViewModel for testing API integration.
/// Displays organizations from the backend API.
/// </summary>
public class ApiTestViewModel : BaseViewModel
{
    private readonly IOrganizationApiService _organizationService;
    private readonly IApiClient _apiClient;
    private readonly IConfigurationService _configurationService;
    private readonly IDialogService _dialogService;
    private ObservableCollection<OrganizationDto> _organizations = new();
    private OrganizationDto? _selectedOrganization;
    private bool _isLoading;
    private string _errorMessage = string.Empty;
    private string _statusMessage = string.Empty;

    public ObservableCollection<OrganizationDto> Organizations
    {
        get => _organizations;
        set => SetProperty(ref _organizations, value);
    }

    public OrganizationDto? SelectedOrganization
    {
        get => _selectedOrganization;
        set
        {
            if (SetProperty(ref _selectedOrganization, value) && value != null)
            {
                _apiClient.SetCurrentOrgId(value.Id);
                _configurationService.SetLocalValue("ApiCurrentOrgId", value.Id.ToString());
                StatusMessage = $"Selected: {value.Name} (Role: {value.CurrentUserRole})";
            }
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public ICommand LoadOrganizationsCommand { get; }
    public ICommand CreateOrganizationCommand { get; }
    public ICommand DeleteSelectedOrganizationCommand { get; }

    public ApiTestViewModel(
        IOrganizationApiService organizationService,
        IConfigurationService configurationService,
        IApiClient apiClient,
        IDialogService dialogService)
    {
        _organizationService = organizationService;
        _configurationService = configurationService;
        _apiClient = apiClient;
        _dialogService = dialogService;

        LoadOrganizationsCommand = new RelayCommand(async _ => await LoadOrganizationsAsync());
        CreateOrganizationCommand = new RelayCommand(async _ => await CreateTestOrganizationAsync());
        DeleteSelectedOrganizationCommand = new RelayCommand(async _ => await DeleteSelectedOrganizationAsync());

        // Auto-load on startup
        _ = LoadOrganizationsAsync();
    }

    private async Task LoadOrganizationsAsync()
    {
        try
        {
            IsLoading = true;
            ErrorMessage = string.Empty;
            StatusMessage = "Loading organizations...";

            var orgs = await _organizationService.GetUserOrganizationsAsync();
            Organizations = new ObservableCollection<OrganizationDto>(orgs);

            if (Organizations.Count > 0)
            {
                OrganizationDto? preferred = null;

                if (SelectedOrganization != null)
                {
                    preferred = Organizations.FirstOrDefault(o => o.Id == SelectedOrganization.Id);
                }

                if (preferred == null && _apiClient.CurrentOrgId.HasValue)
                {
                    preferred = Organizations.FirstOrDefault(o => o.Id == _apiClient.CurrentOrgId.Value);
                }

                if (preferred == null)
                {
                    var persisted = _configurationService.GetValue("ApiCurrentOrgId");
                    if (Guid.TryParse(persisted, out var persistedOrgId))
                    {
                        preferred = Organizations.FirstOrDefault(o => o.Id == persistedOrgId);
                    }
                }

                SelectedOrganization = preferred ?? Organizations[0];
                StatusMessage = $"Loaded {Organizations.Count} organization(s)";
            }
            else
            {
                SelectedOrganization = null;
                _apiClient.ClearCurrentOrgId();
                _configurationService.SetLocalValue("ApiCurrentOrgId", string.Empty);
                StatusMessage = "No organizations found. Click 'Create Test Org' to create one.";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load organizations: {ex.Message}";
            StatusMessage = "Error occurred";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task CreateTestOrganizationAsync()
    {
        try
        {
            IsLoading = true;
            ErrorMessage = string.Empty;
            StatusMessage = "Creating test organization...";

            var timestamp = DateTime.Now.ToString("HHmmss");
            var orgName = $"Test Org {timestamp}";
            
            var newOrg = await _organizationService.CreateOrganizationAsync(orgName);
            Organizations.Add(newOrg);
            SelectedOrganization = newOrg;
            
            StatusMessage = $"✓ Created: {orgName}";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to create organization: {ex.Message}";
            StatusMessage = "Error occurred";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task DeleteSelectedOrganizationAsync()
    {
        if (SelectedOrganization == null)
        {
            StatusMessage = "Select an organization to delete.";
            return;
        }

        if (Organizations.Count <= 1)
        {
            await _dialogService.ShowMessageAsync(
                "Cannot delete the last remaining organization. Create or join another organization first.",
                "Delete Organization");
            return;
        }

        var orgToDelete = SelectedOrganization;
        var confirmed = await _dialogService.ShowConfirmationAsync(
            $"Delete organization '{orgToDelete.Name}'? This will permanently delete all data in that organization.",
            "Delete Organization");

        if (!confirmed)
            return;

        var isActiveOrganization = _apiClient.CurrentOrgId.HasValue && _apiClient.CurrentOrgId.Value == orgToDelete.Id;
        if (isActiveOrganization)
        {
            var typedName = await _dialogService.ShowInputDialogAsync(
                $"You are deleting the currently active organization. Type '{orgToDelete.Name}' to confirm.",
                "Confirm Active Organization Delete");

            if (!string.Equals(typedName?.Trim(), orgToDelete.Name, StringComparison.Ordinal))
            {
                StatusMessage = "Deletion cancelled.";
                return;
            }
        }

        try
        {
            IsLoading = true;
            ErrorMessage = string.Empty;
            StatusMessage = $"Deleting organization '{orgToDelete.Name}'...";

            await _organizationService.DeleteOrganizationAsync(orgToDelete.Id);
            await LoadOrganizationsAsync();

            StatusMessage = $"✓ Deleted: {orgToDelete.Name}";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to delete organization: {ex.Message}";
            StatusMessage = "Error occurred";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
