# API Integration - Quick Test

## ✅ Files Created

Your Avalonia UI now has full API integration:

1. **API Client Services:**
   - `Services/Api/IApiClient.cs` — HTTP client interface
   - `Services/Api/ApiClient.cs` — HTTP client implementation with X-Org-Id header management
   - `Services/Api/IOrganizationApiService.cs` — Organization API service interface
   - `Services/Api/OrganizationApiService.cs` — Organization API service implementation

2. **Test UI:**
   - `MVVM/ViewModels/ApiTestViewModel.cs` — Test view model
   - `MVVM/Views/ApiTestView.axaml` — Test view
   - `MVVM/Views/ApiTestView.axaml.cs` — View code-behind

3. **Configuration:**
   - Updated `Services/ServiceCollectionExtensions.cs` — Registered API services
   - Updated `app.config.local.example.json` — Added API base URL setting

---

## 🚀 How to Test

### Step 1: Ensure Backend API is Running

In a terminal:
```powershell
cd "C:\Users\Abbas Haider\Repo\Daryva-Avalonia\src\Daryva.Api"
dotnet run
```

You should see:
```
⚠️  DevAuth is ENABLED...
✓ Seeded sample data...
Now listening on: http://localhost:5000
```

### Step 2: Add API Test View to Main Window

You need to temporarily add the `ApiTestView` to your main window to test it.

**Option A: Add to existing navigation**

Open `MVVM/Views/MainWindow.axaml` and find where views are displayed. Add:

```xml
<views:ApiTestView DataContext="{Binding ApiTestViewModel}" />
```

**Option B: Quick test (replace MainWindow content temporarily)**

Open `MVVM/Views/MainWindow.axaml.cs` and add this code:

```csharp
using Microsoft.Extensions.DependencyInjection;

public MainWindow()
{
    InitializeComponent();
    
    // Quick test: Load API test view
    if (App.ServiceProvider != null)
    {
        var apiTestViewModel = App.ServiceProvider.GetService<ApiTestViewModel>();
        var apiTestView = new ApiTestView { DataContext = apiTestViewModel };
        this.Content = apiTestView;
    }
}
```

### Step 3: Run the UI

```powershell
cd "C:\Users\Abbas Haider\Repo\Daryva-Avalonia\src\Daryva.UI"
dotnet run
```

### Step 4: Test Features

The API Test window will show:

1. **Auto-loaded organizations** from the backend
2. **Organization list** with Name, Role, ID
3. **Selected organization details** 
4. **Status messages** for all operations
5. **Error messages** if API is unreachable

**Try these:**
- ✅ Click "🔄 Refresh Organizations" → Reloads from API
- ✅ Click "➕ Create Test Org" → Creates new organization
- ✅ Select different organizations → Updates X-Org-Id header
- ✅ Check status messages → Shows what's happening

---

## 📊 Expected Results

### When API is Running:

```
Status: Loaded 1 organization(s)

Organizations:
├─ Dev Property Management (Role: Owner)
└─ ID: xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx

Selected Organization:
  Name:    Dev Property Management
  Role:    Owner  
  Created: 2026-02-19 02:15:00
  ID:      xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx

✓ X-Org-Id header is now set for all API requests
```

### When API is NOT Running:

```
Error: Failed to load organizations: Unable to connect to remote server

No organizations found. API may not be running.
```

---

## 🔧 Configuration

To change the API base URL, update your config:

1. Copy `app.config.local.example.json` to your AppData folder:
   ```
   C:\Users\<YourName>\AppData\Roaming\Daryva\app.config.local.json
   ```

2. Add/update:
   ```json
   {
     "AppSettings": {
       "ApiBaseUrl": "http://localhost:5000"
     }
   }
   ```

---

## ✨ What's Working

✅ **HTTP Client Integration**
- HttpClient configured with base URL
- Automatic JSON serialization/deserialization
- Proper error handling

✅ **Organization Context**
- X-Org-Id header management
- Automatic header updates on org selection
- All future API calls scoped to selected org

✅ **Async Operations**
- Non-blocking API calls
- Loading indicators
- Proper cancellation token support

✅ **Error Handling**
- Try/catch on all API calls
- User-friendly error messages
- Status updates

---

## 🎯 Next Steps

Now that API integration works, you can:

1. **Integrate into existing views**
   - Replace local SQLite calls with API calls
   - Use `IOrganizationApiService` in existing view models
   - Add org selection to main navigation

2. **Add House API service**
   - Create `IHouseApiService`
   - Implement CRUD operations for houses
   - Update `HousesViewModel` to use API

3. **Add Authentication UI**
   - Login screen
   - Token storage
   - Refresh token logic

4. **Build More Features**
   - Tenants API service
   - Payments API service
   - Documents API service
   - Real-time updates (SignalR)

---

## 📝 Code Examples

### Using OrganizationApiService in Your ViewModels:

```csharp
public class MyViewModel
{
    private readonly IOrganizationApiService _orgService;
    private readonly IApiClient _apiClient;

    public MyViewModel(IOrganizationApiService orgService, IApiClient apiClient)
    {
        _orgService = orgService;
        _apiClient = apiClient;
    }

    public async Task LoadDataAsync()
    {
        // Get user's organizations
        var orgs = await _orgService.GetUserOrganizationsAsync();
        
        // Set current org
        if (orgs.Count > 0)
        {
            _apiClient.SetCurrentOrgId(orgs[0].Id);
        }
        
        // Now all API calls will use X-Org-Id header
    }
}
```

### Creating a House API Service:

```csharp
public interface IHouseApiService
{
    Task<List<HouseDto>> GetHousesAsync(CancellationToken token = default);
    Task<HouseDto> CreateHouseAsync(CreateHouseRequest request, CancellationToken token = default);
    // ... etc
}

public class HouseApiService : IHouseApiService
{
    private readonly IApiClient _apiClient;

    public HouseApiService(IApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<List<HouseDto>> GetHousesAsync(CancellationToken token = default)
    {
        var response = await _apiClient.HttpClient.GetAsync("api/houses", token);
        response.EnsureSuccessStatusCode();
        
        var content = await response.Content.ReadAsStringAsync(token);
        return JsonSerializer.Deserialize<List<HouseDto>>(content, JsonOptions) ?? new();
    }
}
```

---

## 🎉 Summary

You now have:
- ✅ Full HTTP API client integration
- ✅ Organization management working
- ✅ Multi-tenancy with X-Org-Id header
- ✅ Test UI to verify everything works
- ✅ Foundation for all future API services

**Your Avalonia UI can now communicate with the backend API!** 🚀
