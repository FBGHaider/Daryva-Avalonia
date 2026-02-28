# PHASE 1 — Auth + state flow audit

## 1) IAuthService / token store / logout

| Component | Location | Notes |
|-----------|----------|--------|
| **IAuthService** | `src/Daryva.UI/Services/Auth/IAuthService.cs` | HasValidSessionAsync, SignInAsync, SignOutAsync, GetAccessTokenAsync, TryRefreshAsync, StateChanged. |
| **AuthService** | `src/Daryva.UI/Services/Auth/AuthService.cs` | OIDC + PKCE; uses ITokenStore + IAuthSessionService. On SignOut: clears TokenStore, AuthSession.ClearSession(), then IAccountDataClearer.ClearAsync(), then RaiseStateChanged(false). |
| **ITokenStore / TokenStore** | `Services/Auth/TokenStore.cs` | Single key `Daryva.SaaS.Tokens` in ISecureStore; Save/Load/Clear. Not user-scoped. |
| **IAuthSessionService** | `Services/Api/AuthSessionService.cs` | In-memory + persisted to secure store as `ApiAuthSession`. Holds AccessToken, RefreshToken, UserId, Email, ExpiresAt. ClearSession() clears all. |
| **Logout flow** | AuthService.SignOutAsync → TokenStore.ClearAsync, AuthSession.ClearSession, AccountDataClearer.ClearAsync, StateChanged(false). MainViewModel.OnAuthStateChanged navigates to SignInViewModel. |

**Finding:** Logout clears tokens and calls AccountDataClearer but does not clear navigation stack, singleton ViewModel state, or all per-user config keys.

---

## 2) OrgContext and where it loads orgs

| Item | Location | Behaviour |
|------|----------|-----------|
| **IOrgContext / OrgContext** | `src/Daryva.UI/Services/OrgContext/OrgContext.cs` | Source of truth for Orgs, CurrentOrgId, RequiresOnboarding, RequiresProfile. |
| **Load orgs** | OrgContext.RefreshAsync() | Calls IMeApiService.GetMeAsync() (GET /api/me). If me != null: _currentUserId = me.User.Id, Orgs from me.Organisations. If me == null: falls back to TryLoadOrgsFromApiAsync (GET /api/orgs) and TryLoadPersistedOrgAsync (config + file by preferred id). |
| **Persistence** | Same file | GetCurrentOrgConfigKey() = `ApiCurrentOrgId` or `ApiCurrentOrgId_<userId>`. ReadPersistedCurrentOrgId() reads `current_org.json` (userId + currentOrgId). SavePersistedCurrentOrg writes that file + SetLocalValue(config key). ClearPersistedCurrentOrg() clears config key and deletes file. |

**Finding:** Org list is correctly from /api/me when me is non-null. Fallbacks (GET /api/orgs, persisted org) can run when /api/me fails and may mix with stale data if session changed.

---

## 3) Where CurrentOrgId is persisted

| Store | Key / file | Who reads/writes | Scoped? |
|-------|------------|------------------|---------|
| **ConfigurationService** | `ApiCurrentOrgId` (global) | ApiClient constructor reads; OrgContext and ApiClient write. AccountDataClearer clears only this key. | No (global) |
| **ConfigurationService** | `ApiCurrentOrgId_<userId>` | OrgContext only (GetCurrentOrgConfigKey). Written on SetCurrentOrgAsync / SavePersistedCurrentOrg. Cleared only via ClearPersistedCurrentOrg() which uses GetCurrentOrgConfigKey() — at sign-out _currentUserId is already null so we clear `ApiCurrentOrgId` only, not `ApiCurrentOrgId_<previousUserId>`. | Per-user key exists but is never cleared on sign-out |
| **AppData file** | `current_org.json` | OrgContext: format `{ userId, currentOrgId }`. AccountDataClearer deletes file on sign-out. OrganisationService also uses file `current_org.json` with format `{ CurrentOrgId }` (no userId). | OrgContext: user-scoped. OrganisationService: global (no userId) |
| **ApiClient** | In-memory _currentOrgId + DefaultRequestHeaders "X-Org-Id" | Set by OrgContext; constructor restores from config `ApiCurrentOrgId` only. | Constructor uses global key only |

**Finding:** Global `ApiCurrentOrgId` is cleared on sign-out; per-user config key `ApiCurrentOrgId_<userId>` is not cleared. OrganisationService persists current org to same filename without userId (can leak across users). ApiClient constructor restores from global key only (correct after clear).

---

## 4) Singletons and cached services that survive sign-out

| Service / VM | Registration | Survives sign-out? | Risk |
|--------------|--------------|--------------------|------|
| IAuthService | Singleton | Yes | OK; stateless for session. |
| IApiClient | Singleton | Yes | OK; ApplyAuthState() and ClearCurrentOrgId() used on sign-out. |
| IOrgContext | Singleton | Yes | ClearForSignOut() clears in-memory state. |
| IAuthSessionService | Singleton | Yes | ClearSession() on sign-out. |
| IConfigurationService | Singleton | Yes | Holds all config keys; per-user keys not cleared. |
| MainViewModel | Singleton | Yes | OnAuthStateChanged navigates to SignIn; does not clear navigation stack or reset _lastDisplayedOrgId / internal state. |
| ProfileMenuViewModel | Singleton | Yes | LoadUserInfoAsync() runs once in ctor; no subscription to auth/session change — can show previous user name/initials. |
| NotificationCenterViewModel | Singleton | Yes | May hold notifications from previous session. |
| INavigationService | Singleton | Yes | NavigateTo&lt;SignInViewModel&gt;() pushes current VM onto _navigationStack; stack is never cleared on sign-out, so old Dashboard/Houses/Tenants VMs from Account A remain in stack. |

**Finding:** Navigation stack keeps references to pre–sign-out ViewModels (e.g. Dashboard, Houses). ProfileMenuViewModel and MainViewModel do not refresh or clear state on session change. Per-user config keys persist in ConfigurationService.

---

## 5) DevAuth and local stub modes

| Item | Location | Behaviour |
|------|----------|-----------|
| **DevAuth** | API: `DevAuthMiddleware.cs`, `appsettings.json`, `appsettings.Development.json` | DevAuth:Enabled read from config. When enabled, unauthenticated requests get injected user (UserId, Email e.g. dev@local). |
| **appsettings.json** | `DevAuth.Enabled: true` | Production/default config has DevAuth ON — risk of dev@local in non-dev environments. |
| **appsettings.Development.json** | `DevAuth.Enabled: true` | Explicit for development. |
| **Seed/mock** | SeedController, DataSeeder, ImportController clear | Gated by DevAuth:Enabled. |

**Finding:** DevAuth is ON in base `appsettings.json`; should be OFF by default and only ON in Development (or explicit flag). No UI-side “dev auth” stub found; leakage is from API when DevAuth is enabled.

---

## Root causes (summary)

1. **Global / mixed org persistence:** ApiClient and OrgContext both persist org id; ApiClient uses only global key; per-user key `ApiCurrentOrgId_<userId>` is never cleared on sign-out; OrganisationService writes `current_org.json` without userId.
2. **Stale ViewModels:** Navigation stack is not cleared on sign-out, so old VMs (Dashboard, Houses, etc.) remain; singleton ProfileMenuViewModel and MainViewModel do not clear or refresh on SessionChanged.
3. **Token usage:** ApiClient uses IAuthSessionService (in-memory) and EnsureFreshTokenIfNeededAsync uses IAuthService.GetAccessTokenAsync — token is not “cached forever” but session state can be stale if not cleared (handled by ClearSession on sign-out).
4. **Dev mode leakage:** DevAuth.Enabled is true in base appsettings.json; should be false for Release/default.

---

## Files touched in audit (read-only)

- `src/Daryva.UI/Services/Auth/IAuthService.cs`
- `src/Daryva.UI/Services/Auth/AuthService.cs`
- `src/Daryva.UI/Services/Auth/AccountDataClearer.cs`
- `src/Daryva.UI/Services/Auth/TokenStore.cs`
- `src/Daryva.UI/Services/Api/IAuthSessionService.cs`
- `src/Daryva.UI/Services/Api/AuthSessionService.cs`
- `src/Daryva.UI/Services/Api/ApiClient.cs`
- `src/Daryva.UI/Services/OrgContext/IOrgContext.cs`
- `src/Daryva.UI/Services/OrgContext/OrgContext.cs`
- `src/Daryva.UI/Services/ServiceCollectionExtensions.cs`
- `src/Daryva.UI/Services/Business/OrganisationService.cs`
- `src/Daryva.UI/Services/ConfigurationService.cs`
- `src/Daryva.UI/Services/Navigation/NavigationService.cs`
- `src/Daryva.UI/MVVM/ViewModels/MainViewModel.cs`
- `src/Daryva.UI/MVVM/ViewModels/ProfileMenuViewModel.cs`
- `src/Daryva.UI/App.axaml.cs`
- `src/Daryva.Api/appsettings.json`
- `src/Daryva.Api/Security/DevAuthMiddleware.cs`

No code changes in Phase 1; this document is the only deliverable.

---

## Phase 2 (Session identity + Scoped storage) — Summary

- **ISessionContext**: UserId, Email, IsAuthenticated, SessionChanged; SetFromToken (on sign-in), UpdateFromMe (from /api/me), Clear (on sign-out).
- **SessionContext**: Backed by IAuthSessionService; updated by AuthService (SetFromToken), OrgContext (UpdateFromMe when me != null), AccountDataClearer (Clear).
- **IScopedStorage / ScopedStorage**: Keys namespaced as `user:{UserId}:{key}` via IConfigurationService; Get/Set/Remove no-op when not authenticated. Used in Phase 4 for currentOrgId.
- **Migration**: Old global current_org handling deferred to Phase 4 (OrgContext will use IScopedStorage and can migrate global key only if it matches current user).

---

## Phase 3 — AppResetService

- **IAppResetService / AppResetService**: Single place for reset; `ResetToSignedOutAsync()` clears SessionContext, OrgContext, ApiClient auth, Navigation stack, NotificationFeedService cache, and local files (orgs.json, current_org.json, members, config key).
- **INavigationService.ClearStackAndCurrent()**: Clears stack and current VM on sign-out so no stale ViewModels remain.
- **INotificationFeedService.ClearForSignOut()**: Clears in-memory read state.
- **AuthService.SignOutAsync**: Calls AppResetService.ResetToSignedOutAsync(); AccountDataClearer delegates to it.

---

## Phase 4 — OrgContext account-bound

- **RefreshAsync**: Requires `SessionContext.IsAuthenticated`; if not authenticated, clears and returns. Orgs from `/api/me` response ONLY (no fallback to GET /api/orgs or persisted org). On user change (`previousUserId != _currentUserId`), clears `_currentOrgId`.
- **Persistence**: Last-selected org via `IScopedStorage` key `currentOrgId` (per-user). File `current_org.json` still written for migration; `ReadPersistedCurrentOrgId` tries ScopedStorage then file.
- **ClearCurrentOrgSelection()**: New method; clears current org only. `ClearForSignOut()` unchanged.
- **OrgContext** no longer depends on IApiClient.

---

## Phase 5 — ApiClient token + X-Org-Id

- **X-Org-Id**: Always from `IOrgContext.CurrentOrgId` (ApiClient injects IOrgContext). Absent when `CurrentOrgId` is null (SignInView/SetupRequired).
- **ApiClient**: No longer restores org from config in constructor. `CurrentOrgId` getter returns `_orgContext.CurrentOrgId`. `SetCurrentOrgId(guid)` delegates to `_orgContext.SetCurrentOrgAsync(guid)`; `ClearCurrentOrgId()` calls `_orgContext.ClearCurrentOrgSelection()`.
- **401**: Existing behaviour kept: refresh once, retry; if still 401 → SignOutAsync.

---

## Phase 6 — ViewModel lifecycle

- **ProfileMenuViewModel**: Subscribes to `IAuthService.StateChanged`; on sign-out clears UserDisplayName/UserInitials/OrgName; on sign-in calls `LoadUserInfoAsync()` again.
- **MainViewModel**: On sign-out sets `_lastDisplayedOrgId = null` so org label refreshes correctly after next sign-in.
- **Dashboard/Houses/Tenants/Documents/RentPayments/Organisation**: Already subscribe to `IOrgContext.CurrentOrgChanged` and reload on org change.

---

## Phase 7 — Dev/local leakage

- **appsettings.json**: `DevAuth.Enabled` set to `false` (production default).
- **Program.cs**: `devAuthEnabled` is true only when `app.Environment.IsDevelopment() && devAuthConfigEnabled` (so Release never uses DevAuth).
- **Temporary debug logs**: `SessionContext` (SetFromToken/UpdateFromMe/Clear), `OrgContext` (/api/me orgs count, CurrentOrgId after selection), `ScopedStorage` (Set key namespaced by user). Remove or downgrade to trace after confirming fix.
