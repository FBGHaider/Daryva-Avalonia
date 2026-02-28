# Desktop SaaS Auth — Phase 1 Audit

This document records the Phase 1 audit for implementing production-grade SaaS authentication in the Daryva Avalonia desktop app.

---

## 1) Where the app starts and chooses MainView

### Entry point
- **`App.axaml.cs`** (`src/Daryva.UI/App.axaml.cs`)
  - `OnFrameworkInitializationCompleted()`:
    1. Builds `ServiceProvider` from `ConfigureServices(serviceCollection)`.
    2. Forces API-only mode via `IConfigurationService.SetLocalValue("DataMode", "Api")`.
    3. Calls `InitializeTheme()` and `InitializeDateFormatAsync()`.
    4. If lifetime is `IClassicDesktopStyleApplicationLifetime`, calls **`InitializeDesktopAsync(desktop)`**.

### Main window and “main view”
- **`InitializeDesktopAsync`** (lines 59–82):
  1. Calls `ApplyLoginPersistencePolicy(serviceProvider)` — if “KeepMeLoggedIn” is false, clears session via `IAuthSessionService.ClearSession()`.
  2. Resolves **`MainWindow`** and **`MainViewModel`** from DI.
  3. Sets `mainWindow.DataContext = mainViewModel`.
  4. Resolves `ScheduledNotificationProcessor` (fire-and-forget).
  5. On UI thread: sets `desktop.MainWindow = mainWindow`, `mainWindow.Show()`.
- There is **no** conditional “show SignIn vs Main” at startup. The same **MainWindow** always shows; its content is driven by **MainViewModel.CurrentViewModel** and **MainViewModel.IsOnboardingMode**.

### How the “main” content is chosen
- **`MainWindow.axaml`**:
  - Single window with a **left nav** (hidden when `IsOnboardingMode` is true) and a **ContentControl** bound to `CurrentViewModel`.
  - When `IsOnboardingMode` is true: full-width `ContentControl` shows the same `CurrentViewModel` (onboarding).
  - When `IsOnboardingMode` is false: main content area shows `CurrentViewModel` (Dashboard, Houses, etc.).
- **`MainViewModel`** constructor (lines 92–94):
  - Calls **`InitializeOrganizationContextAsync()`** and **`RefreshCurrentOrganizationLabelAsync()`** (fire-and-forget).
- **`InitializeOrganizationContextAsync()`** (lines 97–156):
  - If **not** `_authSessionService.IsAuthenticated` → **`NavigateToOnboarding()`** (sets `CurrentViewModel` to `OnboardingViewModel`, `IsOnboardingMode = true`).
  - If authenticated: calls **`_organizationApiService.GetUserOrganizationsAsync()`** (GET api/orgs).
  - Then:
    - If preferred org (from config `ApiCurrentOrgId`) exists in list → set it on `_apiClient`, **NavigateToDashboard()**, load start page.
    - Else if exactly one org → set it, dashboard, load start page.
    - Else if multiple orgs → set first org, dashboard, load start page.
    - Else (no orgs) → **NavigateToOnboarding()**.
  - On any exception → **NavigateToOnboarding()**.

**Summary:** The app always shows **MainWindow** with **MainViewModel**. The effective “main view” is chosen in **MainViewModel.InitializeOrganizationContextAsync()** based on auth session and org list: either **OnboardingViewModel** (sign-in / org selection) or **DashboardViewModel** (and nav). There is no separate “SignInView” or “AppShell”; onboarding is the current sign-in/org flow.

---

## 2) Existing API calling patterns and centralization

### Central client
- **`IApiClient`** / **`ApiClient`** (`src/Daryva.UI/Services/Api/IApiClient.cs`, `ApiClient.cs`):
  - Single **HttpClient** with **BaseAddress** from **`IConfigurationService.GetValue("ApiBaseUrl")`** (default `http://localhost:5000`).
  - **ApiAuthHandler** (DelegatingHandler): adds **Bearer** from **`IAuthSessionService.AccessToken`**; on **401** tries **refresh** via `api/auth/refresh` and **retries once**; refreshes when token expires within 1 minute.
  - **X-Org-Id**: set via **`SetCurrentOrgId(Guid)`** / **`ClearCurrentOrgId()`**; persisted in config as **`ApiCurrentOrgId`**.
  - **ApplyAuthState()**: reapplies Bearer from session (no X-Org-Id change).
  - All API calls go through **`_apiClient.HttpClient`** (relative paths, e.g. `api/orgs`, `api/houses`).

### Call sites (all use `IApiClient.HttpClient`)
- **HouseApiService**: `GetAsync("api/houses" + query)`, `GetAsync($"api/houses/{houseId}")`, `PostAsync("api/houses", ...)`, etc.
- **TenancyApiService**: `PostAsJsonAsync("api/tenancies", ...)`, `GetAsync("api/tenancies" + query)`, etc.
- **TenantApiService**: (similar pattern).
- **PaymentApiService**: `PostAsJsonAsync("api/payments/record", ...)`, `GetAsync(...)`, etc.
- **DocumentApiService**: `GetAsync("api/documents", ...)`, etc.
- **ExpenseApiService**: `GetAsync("api/expenses", ...)`, etc.
- **NotificationApiService**: `GetFromJsonAsync(...)`, `PostAsJsonAsync(...)`, etc.
- **OrganizationApiService**: `GetAsync("api/orgs")`, `PostAsync("api/orgs", ...)`, `GetAsync($"api/orgs/{orgId}")`, etc.
- **AuthApiService**: **`PostAsJsonAsync("api/auth/login", ...)`**, **`GetAsync("api/auth/me")`**, **`PostAsJsonAsync("api/auth/refresh", ...)`** (in ApiClient), **`PostAsJsonAsync("api/auth/logout", ...)`**, register/verify-email/resend.
- **BackupService**, **MigrationService**, **ApiSettingsService**: one-off **GetAsync** / **PostAsync** on `_apiClient.HttpClient`.

### Backend alignment
- Backend **SaaS** endpoints: **GET /api/me** (user + organisations + requiresOrgSetup + requiresProfileSetup). No email/password login there; JWT from OIDC/DevAuth.
- Backend **legacy** auth: **api/auth** (login, register, verify-email, refresh, logout). Desktop currently uses **api/auth/login** and **api/auth/me**; these do not match the SaaS **/api/me** contract.

**Summary:** API usage is already centralized on **IApiClient** + **HttpClient** (with auth handler and X-Org-Id). No HTTP in View code-behind. For SaaS we need to:
- Switch auth to **OIDC + JWT** and use **GET api/me** (not api/auth/me) for current user and org list.
- Keep using the same **IApiClient** for all requests; ensure **Authorization** and **X-Org-Id** are applied consistently (and 401 → refresh once → retry → sign-out).

---

## 3) Existing org switching and replacement with IOrgContext

### Current org state
- **IApiClient** holds **CurrentOrgId** and sets **X-Org-Id** on **HttpClient**; also persists to config **ApiCurrentOrgId**.
- **MainViewModel**:
  - Reads preferred org from **`_configurationService.GetValue("ApiCurrentOrgId")`** and uses **`_organizationApiService.GetUserOrganizationsAsync()`** (GET api/orgs) to resolve org list and selection.
  - **SwitchOrganizationCommand** is bound to **`NavigateToOnboarding()`** — i.e. “Switch Org” **navigates to OnboardingViewModel** (login/register + org list); it does **not** open a dedicated org selector or call a dedicated “set current org” service.

### OnboardingViewModel
- **LoadOrganizationsAsync()**: calls **GetUserOrganizationsAsync()**, fills **Organizations** (ObservableCollection), sets **SelectedOrganization**.
- **UseSelectedOrganization()**: calls **`_apiClient.SetCurrentOrgId(SelectedOrganization.Id)`**, then **`_navigationService.NavigateTo<DashboardViewModel>()`** (and **IsOnboardingMode** is cleared by the nav handler in MainViewModel).
- So “select org” is only from the onboarding screen after login; there is no in-app “org switcher” that changes **CurrentOrgId** without going through onboarding.

### Where to introduce IOrgContext
- **MainViewModel**: replace direct use of **IApiClient.CurrentOrgId**, **IOrganizationApiService.GetUserOrganizationsAsync()**, and config **ApiCurrentOrgId** with **IOrgContext** (CurrentOrgId, Orgs, CurrentOrg, RefreshAsync, SetCurrentOrgAsync, RequiresOnboarding, RequiresProfile).
- **Startup flow**: call **IOrgContext.RefreshAsync()** (which calls GET api/me); then branch on **RequiresOnboarding** / **RequiresProfile** → SetupRequiredView or sign-in; else **CurrentOrgId** (auto-select if one org) → main shell.
- **Org switch UI**: “Switch Org” should call **IOrgContext.SetCurrentOrgAsync(guid)** (and persist), then raise an event so ViewModels (Houses, Tenants, Payments, Documents, etc.) reload; **not** navigate to full onboarding unless we need to re-authenticate or complete setup.
- **IApiClient**: can keep **SetCurrentOrgId** / **CurrentOrgId** for the **X-Org-Id** header, but the **source of truth** for “current org and list” should be **IOrgContext**; ApiClient can take **CurrentOrgId** from **IOrgContext** (injected or event) so one place controls it.

**Summary:** Org state is currently **IApiClient** + config + **MainViewModel** + **OnboardingViewModel** using **IOrganizationApiService**. Replacing this with **IOrgContext** (backed by GET api/me, with persistence of CurrentOrgId) and using it in MainViewModel and for “Switch Org” will centralise org context and align with the SaaS backend.

---

## Files reference (entry points and key types)

| Area | File(s) |
|------|--------|
| App entry / main window | `App.axaml.cs` (InitializeDesktopAsync, ApplyLoginPersistencePolicy) |
| Shell / nav | `MainWindow.axaml`, `MainWindow.axaml.cs`, `MainViewModel.cs` |
| Auth session | `IAuthSessionService.cs`, `AuthSessionService.cs` (uses ISecureStore) |
| API client | `IApiClient.cs`, `ApiClient.cs` (ApiAuthHandler, X-Org-Id, refresh) |
| Auth API (login/me) | `AuthApiService.cs` (api/auth/login, api/auth/me) |
| Orgs API | `OrganizationApiService.cs` (api/orgs) |
| Onboarding / org selection | `OnboardingViewModel.cs` |
| Secure storage | `ISecureStore.cs`, `SecureStore.cs` (DPAPI on Windows, AES file elsewhere) |
| DI | `ServiceCollectionExtensions.cs` (AddApplicationServices, AddViewModels) |

---

## Next phases (brief)

- **Phase 2:** OIDC PKCE (system browser + loopback), TokenStore (reuse/extend ISecureStore), refresh if available.
- **Phase 3:** ApiClient: always Bearer + X-Org-Id from context; 401 → refresh once → retry → sign-out; friendly errors.
- **Phase 4:** IOrgContext + GET api/me; RequiresOnboarding / RequiresProfile; SetupRequiredView; persist CurrentOrgId.
- **Phase 5:** Auth gate UI: SignInView, SetupRequiredView, route gating in shell; Log out.
- **Phase 6:** Org-change event; Houses/Tenants/Payments/Documents reload on org switch.
