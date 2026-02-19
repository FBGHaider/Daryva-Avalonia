using System.Collections.ObjectModel;
using System.Windows.Input;
using Daryva.MVVM.Commands;
using Daryva.Services.Api;

namespace Daryva.MVVM.ViewModels;

/// <summary>
/// ViewModel for testing API integration.
/// Displays organizations from the backend API.
/// </summary>
public class ApiTestViewModel : BaseViewModel
{
    private readonly IOrganizationApiService _organizationService;
    private readonly IApiClient _apiClient;
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

    public ApiTestViewModel(IOrganizationApiService organizationService, IApiClient apiClient)
    {
        _organizationService = organizationService;
        _apiClient = apiClient;

        LoadOrganizationsCommand = new RelayCommand(async _ => await LoadOrganizationsAsync());
        CreateOrganizationCommand = new RelayCommand(async _ => await CreateTestOrganizationAsync());

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
                SelectedOrganization = Organizations[0];
                StatusMessage = $"Loaded {Organizations.Count} organization(s)";
            }
            else
            {
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
}
