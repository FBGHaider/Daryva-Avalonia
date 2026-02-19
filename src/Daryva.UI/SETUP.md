# Daryva UI - Setup Guide

## Overview

The Daryva UI is built with **Avalonia** (cross-platform desktop framework) and uses **MVVM** architecture to communicate with the backend API.

**Current State:**
- ✅ Avalonia UI project structure exists
- ✅ Dependency injection configured
- ✅ MVVM structure in place
- ⏳ API client integration needed
- ⏳ UI screens need implementation

---

## Prerequisites

- .NET 8 SDK
- Visual Studio Code or Visual Studio 2022
- Backend API running on `http://localhost:5000` (see [Backend Setup](../Daryva.Api/QUICKSTART.md))

---

## Step 1: Build the UI Project

```bash
cd src/Daryva.UI
dotnet build
```

Expected output:
```
Daryva succeeded (2.3s) → bin\Debug\net8.0\Daryva.dll
Build succeeded.
```

---

## Step 2: Create API Client Service

Create a new file: `src/Daryva.UI/Services/Api/IApiClient.cs`

```csharp
namespace Daryva.Services.Api;

/// <summary>
/// HTTP client for communicating with Daryva.Api backend.
/// </summary>
public interface IApiClient
{
    /// <summary>
    /// Set the current organization ID for requests.
    /// </summary>
    void SetCurrentOrgId(Guid orgId);

    /// <summary>
    /// Clear the current organization ID.
    /// </summary>
    void ClearCurrentOrgId();

    /// <summary>
    /// Get the HttpClient for direct use.
    /// </summary>
    HttpClient HttpClient { get; }
}
```

Then create the implementation: `src/Daryva.UI/Services/Api/ApiClient.cs`

```csharp
using Avalonia.Controls;

namespace Daryva.Services.Api;

/// <summary>
/// HTTP client for Daryva.Api backend.
/// </summary>
public class ApiClient : IApiClient
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private Guid? _currentOrgId;

    public HttpClient HttpClient => _httpClient;

    public ApiClient(IConfiguration configuration)
    {
        _configuration = configuration;

        var baseAddress = configuration["ApiSettings:BaseUrl"] ?? "http://localhost:5000";
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(baseAddress),
            Timeout = TimeSpan.FromSeconds(30)
        };

        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
    }

    public void SetCurrentOrgId(Guid orgId)
    {
        _currentOrgId = orgId;
        _httpClient.DefaultRequestHeaders.Remove("X-Org-Id");
        _httpClient.DefaultRequestHeaders.Add("X-Org-Id", orgId.ToString());
    }

    public void ClearCurrentOrgId()
    {
        _currentOrgId = null;
        _httpClient.DefaultRequestHeaders.Remove("X-Org-Id");
    }
}
```

---

## Step 3: Create Organization Service (UI Wrapper)

Create: `src/Daryva.UI/Services/Api/IOrganizationApiService.cs`

```csharp
using Daryva.MVVM.Models;

namespace Daryva.Services.Api;

public interface IOrganizationApiService
{
    Task<List<OrganizationDto>> GetUserOrganizationsAsync(CancellationToken cancellationToken = default);
    Task<OrganizationDto> CreateOrganizationAsync(string name, CancellationToken cancellationToken = default);
    Task<OrganizationDto> GetOrganizationAsync(Guid orgId, CancellationToken cancellationToken = default);
}

public class OrganizationDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string CurrentUserRole { get; set; } = string.Empty;
}
```

Then implementation: `src/Daryva.UI/Services/Api/OrganizationApiService.cs`

```csharp
using System.Text.Json;
using Daryva.MVVM.Models;

namespace Daryva.Services.Api;

public class OrganizationApiService : IOrganizationApiService
{
    private readonly IApiClient _apiClient;
    private readonly ILogger<OrganizationApiService> _logger;

    public OrganizationApiService(IApiClient apiClient, ILogger<OrganizationApiService> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    public async Task<List<OrganizationDto>> GetUserOrganizationsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _apiClient.HttpClient.GetAsync("api/orgs", cancellationToken);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var orgs = JsonSerializer.Deserialize<List<OrganizationDto>>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return orgs ?? new();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching organizations");
            throw;
        }
    }

    public async Task<OrganizationDto> CreateOrganizationAsync(string name, CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new { name };
            var content = new StringContent(
                JsonSerializer.Serialize(request),
                System.Text.Encoding.UTF8,
                "application/json"
            );

            var response = await _apiClient.HttpClient.PostAsync("api/orgs", content, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var org = JsonSerializer.Deserialize<OrganizationDto>(responseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return org ?? throw new InvalidOperationException("Failed to create organization");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating organization");
            throw;
        }
    }

    public async Task<OrganizationDto> GetOrganizationAsync(Guid orgId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _apiClient.HttpClient.GetAsync($"api/orgs/{orgId}", cancellationToken);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var org = JsonSerializer.Deserialize<OrganizationDto>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return org ?? throw new InvalidOperationException("Organization not found");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching organization {OrgId}", orgId);
            throw;
        }
    }
}
```

---

## Step 4: Register Services

Update `Services/ServiceCollectionExtensions.cs`:

```csharp
public static void AddApplicationServices(this IServiceCollection services)
{
    // Existing services...
    services.AddSingleton<IConfigurationService, ConfigurationService>();
    
    // Add API services
    services.AddSingleton<IApiClient, ApiClient>();
    services.AddScoped<IOrganizationApiService, OrganizationApiService>();
    
    // Add other services...
}
```

---

## Step 5: Update appsettings.json

Add API configuration to `src/Daryva.UI/appsettings.json`:

```json
{
  "ApiSettings": {
    "BaseUrl": "http://localhost:5000"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  }
}
```

---

## Step 6: Create Organization ViewModel

Create: `src/Daryva.UI/MVVM/ViewModels/OrganizationViewModel.cs`

```csharp
using System.Collections.ObjectModel;
using Daryva.Services.Api;

namespace Daryva.MVVM.ViewModels;

public class OrganizationViewModel : ViewModelBase
{
    private readonly IOrganizationApiService _organizationService;
    private readonly IApiClient _apiClient;
    private ObservableCollection<OrganizationDto> _organizations = new();
    private OrganizationDto? _selectedOrganization;
    private bool _isLoading;
    private string _errorMessage = string.Empty;

    public ObservableCollection<OrganizationDto> Organizations
    {
        get => _organizations;
        set => SetField(ref _organizations, value);
    }

    public OrganizationDto? SelectedOrganization
    {
        get => _selectedOrganization;
        set
        {
            if (SetField(ref _selectedOrganization, value))
            {
                if (value != null)
                {
                    _apiClient.SetCurrentOrgId(value.Id);
                }
            }
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetField(ref _isLoading, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        set => SetField(ref _errorMessage, value);
    }

    public OrganizationViewModel(IOrganizationApiService organizationService, IApiClient apiClient)
    {
        _organizationService = organizationService;
        _apiClient = apiClient;
    }

    public async Task LoadOrganizationsAsync()
    {
        try
        {
            IsLoading = true;
            ErrorMessage = string.Empty;

            var orgs = await _organizationService.GetUserOrganizationsAsync();
            Organizations = new ObservableCollection<OrganizationDto>(orgs);

            if (Organizations.Count > 0)
            {
                SelectedOrganization = Organizations[0];
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load organizations: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task CreateOrganizationAsync(string name)
    {
        try
        {
            IsLoading = true;
            ErrorMessage = string.Empty;

            var org = await _organizationService.CreateOrganizationAsync(name);
            Organizations.Add(org);
            SelectedOrganization = org;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to create organization: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
```

---

## Step 7: Update Main ViewModel

Update `MVVM/ViewModels/MainViewModel.cs` to load organizations on startup:

```csharp
public class MainViewModel : ViewModelBase
{
    private readonly OrganizationViewModel _organizationViewModel;

    public MainViewModel(OrganizationViewModel organizationViewModel)
    {
        _organizationViewModel = organizationViewModel;
    }

    public override async void OnLoaded()
    {
        base.OnLoaded();
        await _organizationViewModel.LoadOrganizationsAsync();
    }
}
```

---

## Step 8: Create Organization View

Create: `src/Daryva.UI/MVVM/Views/OrganizationView.axaml`

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="Daryva.MVVM.Views.OrganizationView">

    <StackPanel Spacing="10" Padding="20">
        <TextBlock Text="Organizations" FontSize="24" FontWeight="Bold" />

        <!-- Loading Indicator -->
        <ProgressBar IsVisible="{Binding IsLoading}" Value="50" Height="4" />

        <!-- Error Message -->
        <TextBlock Text="{Binding ErrorMessage}" 
                   Foreground="Red" 
                   IsVisible="{Binding !ErrorMessage, Converter={StaticResource StringNullOrEmptyConverter}}" />

        <!-- Organization List -->
        <ListBox ItemsSource="{Binding Organizations}"
                 SelectedItem="{Binding SelectedOrganization}"
                 Height="200">
            <ListBox.ItemTemplate>
                <DataTemplate>
                    <TextBlock Text="{Binding Name}">
                        <TextBlock.ToolTip>
                            <TextBlock Text="{Binding CurrentUserRole}" />
                        </TextBlock.ToolTip>
                    </TextBlock>
                </DataTemplate>
            </ListBox.ItemTemplate>
        </ListBox>

        <!-- Selected Organization Info -->
        <StackPanel Spacing="5" IsVisible="{Binding SelectedOrganization}">
            <TextBlock Text="Selected Organization:" FontWeight="Bold" />
            <TextBlock Text="{Binding SelectedOrganization.Name}" />
            <TextBlock Text="{Binding SelectedOrganization.CurrentUserRole, StringFormat='Role: {0}'}" />
        </StackPanel>
    </StackPanel>
</UserControl>
```

And the code-behind:

```csharp
using Avalonia.Controls;
using Daryva.MVVM.ViewModels;

namespace Daryva.MVVM.Views;

public partial class OrganizationView : UserControl
{
    public OrganizationView()
    {
        InitializeComponent();
    }
}
```

---

## Step 9: Run the UI

```bash
cd src/Daryva.UI
dotnet run
```

Expected output:
```
Now starting Avalonia application.
Application started successfully.
```

The UI should:
1. Load and display organizations from the API
2. Show the selected organization details
3. Display any errors if the API is unreachable

---

## Backend Requirements

Ensure the backend API is running:

```bash
cd src/Daryva.Api
dotnet run
```

Backend should output:
```
⚠️  DevAuth is ENABLED...
✓ Seeded sample data...
Now listening on: http://localhost:5000
```

---

## Testing

### Test 1: List Organizations

1. Start backend API
2. Start UI
3. Verify Organizations list is populated
4. Verify organization name is displayed

### Test 2: Create Organization

Add create button to UI:

```csharp
public ICommand CreateOrganizationCommand => new AsyncRelayCommand(
    async () => await _organizationViewModel.CreateOrganizationAsync("New Organization")
);
```

### Test 3: Select Organization

Click different organizations and verify:
- X-Org-Id header is set
- Organization details update
- Houses will load for that org (in next step)

---

## Next Steps

### Phase 1: Organizations UI
- ✅ List organizations
- ✅ Create organization
- ✅ Select organization
- ⏳ Delete organization
- ⏳ Add members

### Phase 2: Houses UI
- ⏳ List houses for selected org
- ⏳ Create house
- ⏳ Edit house
- ⏳ Delete house

### Phase 3: Advanced
- ⏳ Authentication UI
- ⏳ Member management
- ⏳ Settings
- ⏳ Real-time updates (SignalR)

---

## Troubleshooting

### "Failed to load organizations"

**Check:**
1. Backend API is running (`http://localhost:5000`)
2. Database is accessible (`docker-compose ps`)
3. API logs show no errors

### "Unable to connect to remote server"

**Fix:**
1. Update `appsettings.json` BaseUrl:
   ```json
   {
     "ApiSettings": {
       "BaseUrl": "http://localhost:5000"
     }
   }
   ```

### Build errors

**Ensure:** All API DTOs are created in UI project first (from Step 3)

---

## File Checklist

- ✅ `Services/Api/IApiClient.cs`
- ✅ `Services/Api/ApiClient.cs`
- ✅ `Services/Api/IOrganizationApiService.cs`
- ✅ `Services/Api/OrganizationApiService.cs`
- ✅ `MVVM/ViewModels/OrganizationViewModel.cs`
- ✅ `MVVM/Views/OrganizationView.axaml`
- ✅ `MVVM/Views/OrganizationView.axaml.cs`
- ✅ Updated: `Services/ServiceCollectionExtensions.cs`
- ✅ Updated: `appsettings.json`
- ✅ Updated: `MVVM/ViewModels/MainViewModel.cs`

---

## Summary

You now have:
- ✅ API client abstract layer
- ✅ Organization service (API wrapper)
- ✅ Organization view model (UI logic)
- ✅ Organization view (UI)
- ✅ Dependency injection setup
- ✅ Error handling and loading states

**Ready to:** Build additional screens for houses, members, tenants, etc.
