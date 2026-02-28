# Phase 1 — Audit: Notification & Profile Header

## 1) Dashboard header

- **View:** `src/Daryva.UI/MVVM/Views/DashboardView.axaml`
- **Header block:** Lines 44–59 — `Grid ColumnDefinitions="*,Auto"` with:
  - Left: "Dashboard" title + `GreetingText`
  - Right: `TextBox` (class `top-search`, watermark "Search..."), `Button` (Content "🔔", 36×36), `Border` (36×36, circle) with "👤"
- **ViewModel:** `src/Daryva.UI/MVVM/ViewModels/DashboardViewModel.cs` — no notification or profile state/commands yet. Has `INavigationService`, `IDialogService`, `IServiceProvider`, auth services.

## 2) Navigation & dialog

- **INavigationService** (`src/Daryva.UI/Services/Navigation/INavigationService.cs`):
  - `NavigateTo<T>()`, `NavigateTo(BaseViewModel)`, `NavigateBack()`
  - `CurrentViewModel`, `CurrentViewModelChanged`
  - `GetViewModel<T>()`
  - No parameter-based navigation (e.g. no `NavigateToTenant(id)`). For notification targets we navigate to list VMs: `TenantsViewModel`, `HousesViewModel`, `DocumentsViewModel`, `RentPaymentsViewModel`/`TransactionsViewModel`.
- **NavigationService** (`NavigationService.cs`): stack-based, resolves VMs via `IServiceProvider.GetRequiredService<T>()`.
- **IDialogService** (`Daryva.Services.Dialog`): `ShowMessage`, `ShowMessageAsync`, `ShowConfirmationAsync`, `ShowOpenFileDialogAsync`, etc. Dialogs (e.g. AddHouse, AddTenant) are created as `new Window()` and shown with `ShowDialog(mainWindow)` from ViewModels.

## 3) Icon system

- **Current:** Emoji in XAML (🔔, 👤, 🏠, 👥, ⚠, 📄, etc.) and `Button.icon-button` in DesignSystem (transparent bg, hover Surface2).
- **Theme:** `Themes/Theme.axaml` — colors (BrandPrimary, Surface, TextMuted, etc.). `DesignSystem.axaml` — button/text styles.
- **No Lucide** in project; reuse emoji or simple shapes for notification/profile to stay minimal and consistent.

## Summary

- Header is in `DashboardView.axaml`; bell and avatar are plain Button/Border with emoji.
- Use `INavigationService.NavigateTo<T>()` for list targets; no existing “open entity by id” — navigate to list and optionally extend later.
- Use existing `Button.icon-button` and theme brushes for new controls.
- New notification feed service will be named `INotificationFeedService` to avoid clashing with existing `INotificationService` (email/queue).
