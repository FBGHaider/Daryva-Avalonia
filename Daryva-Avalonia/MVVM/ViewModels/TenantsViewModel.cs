using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Daryva.MVVM.Commands;
using Daryva.MVVM.Models;
using Daryva.Services.Business;
using Daryva.Services.Data;
using Daryva.Services.Dialog;
using Daryva.Services.Navigation;
using Microsoft.Extensions.DependencyInjection;

namespace Daryva.MVVM.ViewModels
{
    public class TenantsViewModel : BaseViewModel
    {
        private readonly ITenantService _tenantService;
        private readonly IDialogService _dialogService;
        private readonly IServiceProvider _serviceProvider;
        private readonly ISettingsService _settingsService;
        private readonly INavigationService _navigationService;
        private readonly ITenancyRepository _tenancyRepository;
        private string _searchTerm = string.Empty;
        private Tenant? _selectedTenant;
        private bool _showArchivedOnly = false;

        public TenantsViewModel(ITenantService tenantService, IDialogService dialogService, IServiceProvider serviceProvider, ISettingsService settingsService, INavigationService navigationService, ITenancyRepository tenancyRepository)
        {
            _tenantService = tenantService;
            _dialogService = dialogService;
            _serviceProvider = serviceProvider;
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
            _tenancyRepository = tenancyRepository ?? throw new ArgumentNullException(nameof(tenancyRepository));
            Tenants = new ObservableCollection<Tenant>();

            LoadTenantsCommand = new RelayCommand(async _ => await LoadTenantsAsync());
            SearchCommand = new RelayCommand(async _ => await SearchTenantsAsync());
            AddTenantCommand = new RelayCommand(_ => ShowAddTenantDialog());
            EditTenantCommand = new RelayCommand(_ => ShowEditTenantDialog(), _ => SelectedTenant != null && !ShowArchivedOnly);
            RemoveTenantCommand = new RelayCommand(_ => RemoveTenantAsync(), _ => SelectedTenant != null && !ShowArchivedOnly);
            ViewArchivedCommand = new RelayCommand(_ => ToggleArchivedView());
            DeleteArchivedCommand = new RelayCommand(_ => DeleteArchivedTenantAsync(), _ => SelectedTenant != null && ShowArchivedOnly);
            RecoverTenantCommand = new RelayCommand(_ => RecoverTenantAsync(), _ => SelectedTenant != null && ShowArchivedOnly);

            LoadTenantsCommand.Execute(null);
        }

        public ICommand LoadTenantsCommand { get; }
        public ICommand SearchCommand { get; }
        public ICommand AddTenantCommand { get; }
        public ICommand EditTenantCommand { get; }
        public ICommand RemoveTenantCommand { get; }
        public ICommand ViewArchivedCommand { get; }
        public ICommand DeleteArchivedCommand { get; }
        public ICommand RecoverTenantCommand { get; }

        public ObservableCollection<Tenant> Tenants { get; }

        public string SearchTerm
        {
            get => _searchTerm;
            set
            {
                if (SetProperty(ref _searchTerm, value))
                {
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
                    ((RelayCommand)RemoveTenantCommand).RaiseCanExecuteChanged();
                    ((RelayCommand)DeleteArchivedCommand).RaiseCanExecuteChanged();
                    ((RelayCommand)RecoverTenantCommand).RaiseCanExecuteChanged();
                    LoadTenantsCommand.Execute(null);
                }
            }
        }

        private async Task LoadTenantsAsync()
        {
            try
            {
                var tenants = await _tenantService.GetAllTenantsAsync(includeArchived: ShowArchivedOnly);
                Tenants.Clear();
                foreach (var tenant in tenants)
                {
                    // If showing archived only, filter to only archived tenants
                    // If showing active only, filter to only non-archived tenants
                    if (ShowArchivedOnly && !tenant.IsArchived)
                        continue;
                    if (!ShowArchivedOnly && tenant.IsArchived)
                        continue;
                    
                    Tenants.Add(tenant);
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error loading tenants: {ex.Message}\n\nStack trace: {ex.StackTrace}", "Database Error");
                System.Diagnostics.Debug.WriteLine($"Error loading tenants: {ex}");
            }
        }

        private async Task SearchTenantsAsync()
        {
            if (string.IsNullOrWhiteSpace(SearchTerm))
            {
                await LoadTenantsAsync();
                return;
            }

            try
            {
                var tenants = await _tenantService.SearchTenantsAsync(SearchTerm);
                Tenants.Clear();
                foreach (var tenant in tenants)
                {
                    Tenants.Add(tenant);
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error searching tenants: {ex.Message}", "Database Error");
                System.Diagnostics.Debug.WriteLine($"Error searching tenants: {ex}");
            }
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
                $"Are you sure you want to remove tenant '{SelectedTenant.FullName}'?\n\nLeave date: {leaveDate:dd/MM/yyyy}\n\nThis will end their tenancy and mark them as archived. Their past rent and deposit payments and transaction history will be kept.",
                "Remove Tenant");

            if (!confirmed) return;

            try
            {
                var tenancies = (await _tenancyRepository.GetTenanciesByTenantIdAsync(SelectedTenant.TenantId)).ToList();
                var activeTenancy = tenancies.FirstOrDefault(t =>
                    string.Equals(t.Status, "Active", StringComparison.OrdinalIgnoreCase) && t.MoveOutDate == null);
                if (activeTenancy != null)
                    await _tenancyRepository.EndTenancyAsync(activeTenancy.TenancyId, leaveDate);

                await _tenantService.ArchiveTenantAsync(SelectedTenant.TenantId);
                _dialogService.ShowMessage("Tenant removed successfully.", "Success");
                LoadTenantsCommand.Execute(null);

                var rentPaymentsVm = _navigationService.GetViewModel<RentPaymentsViewModel>();
                rentPaymentsVm?.LedgerViewModel.LoadLedgerCommand.Execute(null);
                DashboardViewModel.NotifyPaymentDataChanged();
                var dashboardVm = _navigationService.GetViewModel<DashboardViewModel>();
                dashboardVm?.RefreshDashboard();
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error removing tenant: {ex.Message}", "Error");
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
                $"Recover tenant '{SelectedTenant.FullName}'?\n\nThey will appear in the active tenants list again. All their history (tenancies, payments, documents) remains unchanged.",
                "Recover Tenant");

            if (!confirmed) return;

            try
            {
                var tenantId = SelectedTenant.TenantId;
                await _tenantService.UnarchiveTenantAsync(tenantId);

                // Reactivate their most recent ended tenancy so there is only one (active) tenancy—no duplicate
                var tenancies = (await _tenancyRepository.GetTenanciesByTenantIdAsync(tenantId)).ToList();
                var mostRecentEnded = tenancies
                    .Where(t => string.Equals(t.Status, "Ended", StringComparison.OrdinalIgnoreCase) && t.MoveOutDate.HasValue)
                    .OrderByDescending(t => t.MoveOutDate)
                    .FirstOrDefault();
                if (mostRecentEnded != null)
                    await _tenancyRepository.ReactivateTenancyAsync(mostRecentEnded.TenancyId);

                _dialogService.ShowMessage("Tenant recovered. They now appear in the active tenants list.", "Success");
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
    }
}
