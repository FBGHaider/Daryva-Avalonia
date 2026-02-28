# Phase 1 — Organisation / Team audit

## 1) Existing organisation concept

- **MainWindow.axaml**: Sidebar shows "Organization" label bound to `CurrentOrganizationName`, and a "Switch Org" button bound to `SwitchOrganizationCommand`.
- **MainViewModel**: `CurrentOrganizationName`, `SwitchOrganizationCommand` → `NavigateToOnboarding()` (org picker is on Onboarding screen). `RefreshCurrentOrganizationLabelAsync()` loads org name via `IOrganizationApiService.GetOrganizationAsync(_apiClient.CurrentOrgId)`.
- **OnboardingViewModel**: Uses `IOrganizationApiService.GetUserOrganizationsAsync()`, `CreateOrganizationAsync()`, `UseSelectedOrganization()` → `_apiClient.SetCurrentOrgId(SelectedOrganization.Id)`, Join by code/invite.
- **Profile dropdown**: "Organisation / Team" → `NavigateTo<OrganisationViewModel>()` (placeholder "Coming soon").

## 2) Where current org is stored

- **IApiClient** (ApiClient): `CurrentOrgId` (Guid?), `SetCurrentOrgId(Guid)`, `ClearCurrentOrgId()`. Persisted via `IConfigurationService.SetLocalValue("ApiCurrentOrgId", value)` (local config, likely AppData).
- No separate "current org" file; persistence is through existing configuration.

## 3) Reuse vs replace

- **Decision**: Add new **IOrganisationService** and **IOrganisationMemberService** with **local JSON** persistence (AppData) for the Organisation page MVP. This keeps the page working locally and allows a future backend to replace the implementation.
- **Integration**: When user switches/creates/renames org on the Organisation page, call **IApiClient.SetCurrentOrgId** so the rest of the app and API continue to use the selected org. Optionally have MainViewModel resolve current org name from IOrganisationService when available so the sidebar label works for local orgs.
