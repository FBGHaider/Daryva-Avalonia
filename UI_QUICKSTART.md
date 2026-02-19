# Avalonia UI - Quick Start

## TL;DR - 3 Steps

### 1. Start Backend API

```bash
# From repository root:
cd "C:\Users\Abbas Haider\Repo\Daryva-Avalonia\src\Daryva.Api"
dotnet run

# Or if already in repo:
cd src/Daryva.Api
dotnet run
```

### 2. Start UI (New Terminal)

```bash
# From repository root:
cd "C:\Users\Abbas Haider\Repo\Daryva-Avalonia\src\Daryva.UI"
dotnet build
dotnet run

# Or if already in repo:
cd src/Daryva.UI
dotnet build
dotnet run
```

### 3. Test

The UI should load and display:
- List of organizations
- Selected organization details
- API communication working

---

## What You Get

The UI will:
- ✅ Connect to backend API
- ✅ Display organizations loaded from API
- ✅ Support organization selection
- ✅ Set X-Org-Id header automatically
- ✅ Show error messages on API failure

---

## Project Structure

```
src/Daryva.UI/
├── MVVM/
│   ├── ViewModels/     ← Business logic
│   ├── Views/          ← XAML UI
│   └── Models/         ← View models
├── Services/
│   ├── Api/            ← API communication
│   └── Theme/          ← Other services
├── App.axaml           ← App shell
├── Program.cs          ← Startup
├── appsettings.json    ← Config
└── Daryva.csproj       ← Project file
```

---

## Architecture

Request flow:

```
User clicks "Select Org"
  ↓
OrganizationViewModel.SelectedOrganization = org
  ↓
ApiClient.SetCurrentOrgId(org.Id)
  ↓
HttpClient adds header: X-Org-Id: <org-id>
  ↓
All subsequent API calls auto-include org context
```

---

## Next Steps

1. **Create API client services** (see [SETUP.md](SETUP.md))
2. **Build organization screens**
3. **Build house management screens**
4. **Add authentication UI**
5. **Deploy with backend**

See [SETUP.md](SETUP.md) for step-by-step implementation.

---

## Key Files to Create

When following [SETUP.md](SETUP.md), you'll create:

- `Services/Api/IApiClient.cs`
- `Services/Api/ApiClient.cs`
- `Services/Api/IOrganizationApiService.cs`
- `Services/Api/OrganizationApiService.cs`
- `MVVM/ViewModels/OrganizationViewModel.cs`
- `MVVM/Views/OrganizationView.axaml`

---

**Ready to build!** 🚀

See [SETUP.md](SETUP.md) for detailed implementation guide.
