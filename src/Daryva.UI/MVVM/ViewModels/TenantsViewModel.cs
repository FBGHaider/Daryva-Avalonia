using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Daryva.MVVM.Commands;
using Daryva.MVVM.Models;
using Daryva.Services;
using Daryva.Services.Business;
using Daryva.Services.Data;
using Daryva.Services.Dialog;
using Daryva.Services.Navigation;
using Daryva.Services.OrgContext;
using Microsoft.Extensions.DependencyInjection;

namespace Daryva.MVVM.ViewModels
{
    /// <summary>Item for the house filter combo: "All tenants" or a specific house.</summary>
    public class HouseFilterItem
    {
        public string Display { get; set; } = string.Empty;
        public int? HouseId { get; set; }
    }

    public class TenantsViewModel : BaseViewModel, INavigationAware
    {
        private readonly ITenantService _tenantService;
        private readonly IHouseService _houseService;
        private readonly IDialogService _dialogService;
        private readonly IServiceProvider _serviceProvider;
        private readonly ISettingsService _settingsService;
        private readonly INavigationService _navigationService;
        private readonly ITenancyRepository _tenancyRepository;
        private readonly IPaymentService _paymentService;
        private readonly IOrgContext _orgContext;
        private readonly AsyncDebouncer _orgChangeDebouncer = new(TimeSpan.FromMilliseconds(400));
        private string _searchTerm = string.Empty;
        private Tenant? _selectedTenant;
        private bool _showArchivedOnly = false;
        private HouseFilterItem? _selectedHouseFilter;
        private bool _isLoading;

        public TenantsViewModel(ITenantService tenantService, IHouseService houseService, IDialogService dialogService, IServiceProvider serviceProvider, ISettingsService settingsService, INavigationService navigationService, ITenancyRepository tenancyRepository, IPaymentService paymentService, IOrgContext orgContext)
        {
            _tenantService = tenantService;
            _houseService = houseService ?? throw new ArgumentNullException(nameof(houseService));
            _dialogService = dialogService;
            _serviceProvider = serviceProvider;
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
            _tenancyRepository = tenancyRepository ?? throw new ArgumentNullException(nameof(tenancyRepository));
            _paymentService = paymentService ?? throw new ArgumentNullException(nameof(paymentService));
            _orgContext = orgContext ?? throw new ArgumentNullException(nameof(orgContext));
            Tenants = new ObservableCollection<Tenant>();
            DepositReturnList = new ObservableCollection<DepositReturnReminderItem>();
            HouseFilterOptions = new ObservableCollection<HouseFilterItem>();

            LoadTenantsCommand = new RelayCommand(async _ => await LoadTenantsAsync());
            SearchCommand = new RelayCommand(async _ => await SearchTenantsAsync());
            ClearSearchCommand = new RelayCommand(_ => SearchTerm = string.Empty, _ => !string.IsNullOrWhiteSpace(SearchTerm));
            AddTenantCommand = new RelayCommand(_ => ShowAddTenantDialog());
            EditTenantCommand = new RelayCommand(_ => ShowEditTenantDialog(), _ => SelectedTenant != null && !ShowArchivedOnly);
            InviteTenantCommand = new RelayCommand(_ => InviteTenantAsync(), _ => SelectedTenant != null && !ShowArchivedOnly);
            RemoveTenantCommand = new RelayCommand(_ => RemoveTenantAsync(), _ => SelectedTenant != null && !ShowArchivedOnly);
            ViewArchivedCommand = new RelayCommand(_ => ToggleArchivedView());
            DeleteArchivedCommand = new RelayCommand(_ => DeleteArchivedTenantAsync(), _ => SelectedTenant != null && ShowArchivedOnly);
            RecoverTenantCommand = new RelayCommand(_ => RecoverTenantAsync(), _ => SelectedTenant != null && ShowArchivedOnly);
            LoadDepositReturnsCommand = new RelayCommand(async _ => await LoadDepositReturnsAsync());
            RecordDepositReturnedCommand = new RelayCommand(async p => await RecordDepositReturnedAsync(p as DepositReturnReminderItem), p => p is DepositReturnReminderItem);

            _orgContext.CurrentOrgChanged += OnCurrentOrgChanged;
            _ = LoadHouseFilterAsync();
            LoadTenantsCommand.Execute(null);
        }

        public bool NoOrgSelected => !_orgContext.CurrentOrgId.HasValue;

        private void OnCurrentOrgChanged(object? sender, CurrentOrgChangedEventArgs e)
        {
            _orgChangeDebouncer.Trigger(() => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                OnPropertyChanged(nameof(NoOrgSelected));
                LoadTenantsCommand.Execute(null);
            }));
        }

        public void Cleanup()
        {
            _orgContext.CurrentOrgChanged -= OnCurrentOrgChanged;
        }

        public ICommand LoadTenantsCommand { get; }
        public ICommand SearchCommand { get; }
        public ICommand ClearSearchCommand { get; }
        public ICommand AddTenantCommand { get; }
        public ICommand EditTenantCommand { get; }
        public ICommand InviteTenantCommand { get; }
        public ICommand RemoveTenantCommand { get; }
        public ICommand ViewArchivedCommand { get; }
        public ICommand DeleteArchivedCommand { get; }
        public ICommand RecoverTenantCommand { get; }
        public ICommand LoadDepositReturnsCommand { get; }
        public ICommand RecordDepositReturnedCommand { get; }

        public ObservableCollection<Tenant> Tenants { get; }
        public ObservableCollection<DepositReturnReminderItem> DepositReturnList { get; }
        public ObservableCollection<HouseFilterItem> HouseFilterOptions { get; }

        public HouseFilterItem? SelectedHouseFilter
        {
            get => _selectedHouseFilter;
            set
            {
                if (SetProperty(ref _selectedHouseFilter, value))
                {
                    LoadTenantsCommand.Execute(null);
                }
            }
        }

        public string SearchTerm
        {
            get => _searchTerm;
            set
            {
                if (SetProperty(ref _searchTerm, value))
                {
                    ((RelayCommand)ClearSearchCommand).RaiseCanExecuteChanged();
                    SearchCommand.Execute(null);
                }
            }
        }

        public Tenant? SelectedTenant
        {
            get => _selectedTenant;
            set
            {
                if (SetProperty(ref _selectedTenant, value))
                {
                    ((RelayCommand)EditTenantCommand).RaiseCanExecuteChanged();
                    ((RelayCommand)InviteTenantCommand).RaiseCanExecuteChanged();
                    ((RelayCommand)RemoveTenantCommand).RaiseCanExecuteChanged();
                    ((RelayCommand)DeleteArchivedCommand).RaiseCanExecuteChanged();
                    ((RelayCommand)RecoverTenantCommand).RaiseCanExecuteChanged();
                }
            }
        }

        public bool ShowArchivedOnly
        {
            get => _showArchivedOnly;
            set
            {
                if (SetProperty(ref _showArchivedOnly, value))
                {
                    ((RelayCommand)EditTenantCommand).RaiseCanExecuteChanged();
                    ((RelayCommand)InviteTenantCommand).RaiseCanExecuteChanged();
                    ((RelayCommand)RemoveTenantCommand).RaiseCanExecuteChanged();
                    ((RelayCommand)DeleteArchivedCommand).RaiseCanExecuteChanged();
                    ((RelayCommand)RecoverTenantCommand).RaiseCanExecuteChanged();
                    LoadTenantsCommand.Execute(null);
                    if (value) LoadDepositReturnsCommand.Execute(null);
                    NotifyCountsChanged();
                }
            }
        }

        /// <summary>Active tenants count (when not showing archived). For command bar subtitle.</summary>
        public int ActiveCount => ShowArchivedOnly ? 0 : Tenants.Count;
        /// <summary>Placeholder for future: tenants leaving soon.</summary>
        public int LeavingSoonCount => 0;
        /// <summary>Placeholder for future: tenants with overdue rent.</summary>
        public int OverdueCount => 0;
        /// <summary>Formatted subtitle for command bar: e.g. "5 active • 0 leaving soon • 0 overdue".</summary>
        public string SubtitleText => $"{ActiveCount} active • {LeavingSoonCount} leaving soon • {OverdueCount} overdue";
        /// <summary>True when Tenants has items (for empty state visibility).</summary>
        public bool HasTenants => Tenants.Count > 0;

        public bool IsLoading
        {
            get => _isLoading;
            private set => SetProperty(ref _isLoading, value);
        }

        private void NotifyCountsChanged()
        {
            OnPropertyChanged(nameof(ActiveCount));
            OnPropertyChanged(nameof(LeavingSoonCount));
            OnPropertyChanged(nameof(OverdueCount));
            OnPropertyChanged(nameof(SubtitleText));
            OnPropertyChanged(nameof(HasTenants));
        }

        private async Task LoadHouseFilterAsync()
        {
            try
            {
                var houses = (await _houseService.GetAllHousesAsync()).ToList();
                HouseFilterOptions.Clear();
                HouseFilterOptions.Add(new HouseFilterItem { Display = "All tenants", HouseId = null });
                foreach (var h in houses)
                {
                    HouseFilterOptions.Add(new HouseFilterItem { Display = $"{h.AddressLine1}, {h.City}".Trim(',', ' '), HouseId = h.HouseId });
                }
                if (_selectedHouseFilter == null && HouseFilterOptions.Count > 0)
                {
                    _selectedHouseFilter = HouseFilterOptions[0];
                    OnPropertyChanged(nameof(SelectedHouseFilter));
                }
            }
            catch
            {
                HouseFilterOptions.Clear();
                HouseFilterOptions.Add(new HouseFilterItem { Display = "All tenants", HouseId = null });
            }
        }

        private async Task LoadDepositReturnsAsync()
        {
            try
            {
                var list = (await _paymentService.GetDepositReturnRemindersAsync()).ToList();
                DepositReturnList.Clear();
                foreach (var item in list)
                    DepositReturnList.Add(item);
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error loading deposit returns: {ex.Message}", "Error");
            }
        }

        private async Task RecordDepositReturnedAsync(DepositReturnReminderItem? item)
        {
            if (item == null) return;
            var dateFormat = await _settingsService.GetSettingAsync("DateFormat", "dd/MM/yyyy") ?? "dd/MM/yyyy";
            var defaultDate = DateTime.Today.ToString(dateFormat);
            var dateStr = await _dialogService.ShowInputDialogAsync("Date deposit was paid back to tenant:", "Record deposit returned", defaultDate);
            if (string.IsNullOrWhiteSpace(dateStr)) return;
            if (!DateTime.TryParse(dateStr, System.Globalization.CultureInfo.CurrentCulture, System.Globalization.DateTimeStyles.None, out var returnedDate))
            {
                _dialogService.ShowMessage("Please enter a valid date.", "Invalid Date");
                return;
            }
            var amountStr = await _dialogService.ShowInputDialogAsync("Amount returned (£):", "Record deposit returned", item.AmountToReturn.ToString("N2"));
            if (string.IsNullOrWhiteSpace(amountStr)) return;
            if (!decimal.TryParse(amountStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.CurrentCulture, out var amountReturned) || amountReturned < 0)
            {
                _dialogService.ShowMessage("Please enter a valid amount.", "Invalid Amount");
                return;
            }
            try
            {
                await _paymentService.RecordDepositReturnedAsync(item.TenancyId, returnedDate, amountReturned, null);
                _dialogService.ShowMessage("Deposit returned recorded. The tenant will no longer appear in deposit return list and deposit ledger for that month onwards.", "Success");
                await LoadDepositReturnsAsync();
                var dashboardVm = _navigationService.GetViewModel<DashboardViewModel>();
                dashboardVm?.RefreshDashboard();
                var rentPaymentsVm = _navigationService.GetViewModel<RentPaymentsViewModel>();
                rentPaymentsVm?.LedgerViewModel?.LoadDepositLedgerCommand?.Execute(null);
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error recording deposit returned: {ex.Message}", "Error");
            }
        }

        private async Task LoadTenantsAsync()
        {
            if (!_orgContext.CurrentOrgId.HasValue)
            {
                Tenants.Clear();
                OnPropertyChanged(nameof(NoOrgSelected));
                return;
            }
            try
            {
                var tenants = await GetFilteredTenantsAsync();
                Tenants.Clear();
                foreach (var tenant in tenants)
                {
                    Tenants.Add(tenant);
                }
                NotifyCountsChanged();
            }
            catch (Exception ex)
            {
                if (!IsActive)
                {
                    AppLogger.Log("Tenants", $"Suppressing error dialog for abandoned load (navigated away): {ex.Message}");
                    return;
                }
                _dialogService.ShowMessage($"Error loading tenants: {ex.Message}\n\nStack trace: {ex.StackTrace}", "Database Error");
                System.Diagnostics.Debug.WriteLine($"Error loading tenants: {ex}");
            }
        }

        private async Task SearchTenantsAsync()
        {
            try
            {
                IsLoading = true;
                var tenants = await GetFilteredTenantsAsync();
                Tenants.Clear();
                foreach (var tenant in tenants)
                    Tenants.Add(tenant);
                NotifyCountsChanged();
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error searching tenants: {ex.Message}", "Database Error");
                System.Diagnostics.Debug.WriteLine($"Error searching tenants: {ex}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task<IEnumerable<Tenant>> GetFilteredTenantsAsync()
        {
            var houseId = SelectedHouseFilter?.HouseId;
            var tenants = await _tenantService.GetTenantsByHouseIdAsync(houseId, includeArchived: true);

            tenants = ShowArchivedOnly
                ? tenants.Where(t => t.IsArchived)
                : tenants.Where(t => !t.IsArchived);

            if (!string.IsNullOrWhiteSpace(SearchTerm))
            {
                var term = SearchTerm.Trim().ToLowerInvariant();
                tenants = tenants.Where(t =>
                    (t.FullName?.ToLowerInvariant().Contains(term) == true) ||
                    (t.Email?.ToLowerInvariant().Contains(term) == true) ||
                    (t.PhoneNumber?.ToLowerInvariant().Contains(term) == true) ||
                    (t.UniversityName?.ToLowerInvariant().Contains(term) == true));
            }

            return tenants;
        }

        private async void ShowAddTenantDialog()
        {
            try
            {
                var viewModel = _serviceProvider.GetRequiredService<AddTenantViewModel>();
                var dialog = new MVVM.Views.AddTenantDialog(viewModel);
                var mainWindow = Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop 
                    ? desktop.MainWindow 
                    : null;
                if (mainWindow != null)
                {
                    dialog.WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterOwner;
                    await dialog.ShowDialog(mainWindow);
                }
                else
                {
                    dialog.WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterScreen;
                    dialog.Show();
                }
                await LoadTenantsAsync();
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error opening add tenant dialog: {ex.Message}", "Error");
                System.Diagnostics.Debug.WriteLine($"Error: {ex}");
            }
        }

        private async void ShowEditTenantDialog()
        {
            if (SelectedTenant == null) return;

            try
            {
                var viewModel = _serviceProvider.GetRequiredService<EditTenantViewModel>();
                await viewModel.LoadTenantAsync(SelectedTenant);
                var dialog = new MVVM.Views.EditTenantDialog(viewModel);
                var mainWindow = Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop 
                    ? desktop.MainWindow 
                    : null;
                if (mainWindow != null)
                {
                    dialog.WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterOwner;
                    await dialog.ShowDialog(mainWindow);
                }
                else
                {
                    dialog.WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterScreen;
                    dialog.Show();
                }
                await LoadTenantsAsync();
                // Refresh Rent Ledger so rent start changes (e.g. "Next month after move-in") are reflected
                var rentPaymentsVm = _navigationService.GetViewModel<RentPaymentsViewModel>();
                rentPaymentsVm?.LedgerViewModel.LoadLedgerCommand.Execute(null);
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error opening edit tenant dialog: {ex.Message}", "Error");
                System.Diagnostics.Debug.WriteLine($"Error: {ex}");
            }
        }

        private async void RemoveTenantAsync()
        {
            if (SelectedTenant == null) return;

            var dateFormat = await _settingsService.GetSettingAsync("DateFormat", "dd/MM/yyyy") ?? "dd/MM/yyyy";
            var defaultLeaveDate = DateTime.Today.ToString(dateFormat);
            var leaveDateStr = await _dialogService.ShowInputDialogAsync(
                "Enter the tenant's leave date:",
                "Leave Date",
                defaultLeaveDate);
            if (string.IsNullOrWhiteSpace(leaveDateStr))
                return;

            if (!DateTime.TryParse(leaveDateStr, System.Globalization.CultureInfo.CurrentCulture, System.Globalization.DateTimeStyles.None, out var leaveDate))
            {
                _dialogService.ShowMessage("Please enter a valid date.", "Invalid Date");
                return;
            }

            var requireConfirm = await _settingsService.GetSettingAsync<bool>("ConfirmDestructiveActions", true) ?? true;
            var confirmed = !requireConfirm || await _dialogService.ShowConfirmationAsync(
                $"Mark '{SelectedTenant.FullName}' as left?\n\nLeave date: {leaveDate:dd/MM/yyyy}\n\nThey will move to the Leave tab. Past rent, deposit payments and transaction history are kept.",
                "Mark as leave");

            if (!confirmed) return;

            try
            {
                // Mark as leave = end tenancy + archive (shows in Leave tab). Do NOT call DeleteTenantAsync — that wipes payment history.
                var tenancies = (await _tenancyRepository.GetTenanciesByTenantIdAsync(SelectedTenant.TenantId)).ToList();
                var activeTenancy = tenancies.FirstOrDefault(t =>
                    string.Equals(t.Status, "Active", StringComparison.OrdinalIgnoreCase) && t.MoveOutDate == null);
                if (activeTenancy != null)
                    await _tenancyRepository.EndTenancyAsync(activeTenancy.TenancyId, leaveDate);

                await _tenantService.ArchiveTenantAsync(SelectedTenant.TenantId);
                _dialogService.ShowMessage("Tenant marked as left. They now appear in the Leave tab.", "Success");
                LoadTenantsCommand.Execute(null);

                var rentPaymentsVm = _navigationService.GetViewModel<RentPaymentsViewModel>();
                rentPaymentsVm?.LedgerViewModel.LoadLedgerCommand.Execute(null);
                DashboardViewModel.NotifyPaymentDataChanged();
                var dashboardVm = _navigationService.GetViewModel<DashboardViewModel>();
                dashboardVm?.RefreshDashboard();
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error marking tenant as left: {ex.Message}", "Error");
            }
        }

        private void ToggleArchivedView()
        {
            ShowArchivedOnly = !ShowArchivedOnly;
            SelectedTenant = null; // Clear selection when switching views
        }

        private async void DeleteArchivedTenantAsync()
        {
            if (SelectedTenant == null || !SelectedTenant.IsArchived) return;

            var requireConfirm = await _settingsService.GetSettingAsync<bool>("ConfirmDestructiveActions", true) ?? true;
            var confirmed = !requireConfirm || await _dialogService.ShowConfirmationAsync(
                $"Are you sure you want to PERMANENTLY DELETE tenant '{SelectedTenant.FullName}'?\n\n" +
                "This will permanently delete:\n" +
                "- The tenant record\n" +
                "- All associated tenancies\n" +
                "- All payment history\n" +
                "- All documents\n\n" +
                "This action CANNOT be undone!",
                "Delete Tenant Permanently");

            if (!confirmed) return;

            try
            {
                await _tenantService.DeleteTenantAsync(SelectedTenant.TenantId);
                _dialogService.ShowMessage("Tenant deleted permanently.", "Success");
                SelectedTenant = null;
                LoadTenantsCommand.Execute(null);
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error deleting tenant: {ex.Message}", "Error");
                System.Diagnostics.Debug.WriteLine($"Error deleting tenant: {ex}");
            }
        }

        private async void RecoverTenantAsync()
        {
            if (SelectedTenant == null || !SelectedTenant.IsArchived) return;

            var confirmed = await _dialogService.ShowConfirmationAsync(
                $"Recover '{SelectedTenant.FullName}'?\n\nThey will appear in Active Tenants again. All history (tenancies, payments, documents) is unchanged.",
                "Recover");

            if (!confirmed) return;

            try
            {
                var tenantId = SelectedTenant.TenantId;
                await _tenantService.UnarchiveTenantAsync(tenantId);

                try
                {
                    var tenancies = (await _tenancyRepository.GetTenanciesByTenantIdAsync(tenantId)).ToList();
                    var mostRecentEnded = tenancies
                        .Where(t => string.Equals(t.Status, "Ended", StringComparison.OrdinalIgnoreCase) && t.MoveOutDate.HasValue)
                        .OrderByDescending(t => t.MoveOutDate)
                        .FirstOrDefault();
                    if (mostRecentEnded != null)
                        await _tenancyRepository.ReactivateTenancyAsync(mostRecentEnded.TenancyId);
                }
                catch (Exception tenancyEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Tenancy reactivation skipped: {tenancyEx.Message}");
                }

                _dialogService.ShowMessage("Tenant recovered. They now appear in Active Tenants.", "Success");
                SelectedTenant = null;
                LoadTenantsCommand.Execute(null);
                DashboardViewModel.NotifyPaymentDataChanged();
                var dashboardVm = _navigationService.GetViewModel<DashboardViewModel>();
                dashboardVm?.RefreshDashboard();
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error recovering tenant: {ex.Message}", "Error");
            }
        }

        private async void InviteTenantAsync()
        {
            if (SelectedTenant == null) return;

            if (string.IsNullOrWhiteSpace(SelectedTenant.Email))
            {
                _dialogService.ShowMessage(
                    "This tenant has no email address on file. Add one before sending a portal invite.",
                    "No Email Address");
                return;
            }

            var confirmed = await _dialogService.ShowConfirmationAsync(
                $"Send '{SelectedTenant.FullName}' an email invite to set up their tenant portal login?\n\nThey'll be able to view their tenancy documents online without installing the desktop app.",
                "Invite to Portal");

            if (!confirmed) return;

            try
            {
                var email = SelectedTenant.Email;
                await _tenantService.InviteTenantAsync(SelectedTenant.TenantId);
                _dialogService.ShowMessage($"Invite sent to {email}.", "Invite Sent");

                // Tenant is a plain model (no INotifyPropertyChanged), so PortalStatus won't
                // re-evaluate from an in-place mutation -- reload from the API like every other
                // mutating action in this view (see RecoverTenantAsync) so the Portal column
                // reflects "Invited" without needing an app restart.
                SelectedTenant = null;
                LoadTenantsCommand.Execute(null);
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error sending invite: {ex.Message}", "Error");
            }
        }
    }
}
