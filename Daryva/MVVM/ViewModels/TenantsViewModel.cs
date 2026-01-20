using System.Collections.ObjectModel;
using System.Windows.Input;
using Daryva.MVVM.Commands;
using Daryva.MVVM.Models;
using Daryva.Services.Business;
using Daryva.Services.Dialog;
using Microsoft.Extensions.DependencyInjection;

namespace Daryva.MVVM.ViewModels
{
    public class TenantsViewModel : BaseViewModel
    {
        private readonly ITenantService _tenantService;
        private readonly IDialogService _dialogService;
        private readonly IServiceProvider _serviceProvider;
        private string _searchTerm = string.Empty;
        private Tenant? _selectedTenant;

        public TenantsViewModel(ITenantService tenantService, IDialogService dialogService, IServiceProvider serviceProvider)
        {
            _tenantService = tenantService;
            _dialogService = dialogService;
            _serviceProvider = serviceProvider;
            Tenants = new ObservableCollection<Tenant>();

            LoadTenantsCommand = new RelayCommand(async _ => await LoadTenantsAsync());
            SearchCommand = new RelayCommand(async _ => await SearchTenantsAsync());
            AddTenantCommand = new RelayCommand(_ => ShowAddTenantDialog());
            EditTenantCommand = new RelayCommand(_ => ShowEditTenantDialog(), _ => SelectedTenant != null);
            RemoveTenantCommand = new RelayCommand(_ => RemoveTenantAsync(), _ => SelectedTenant != null);

            LoadTenantsCommand.Execute(null);
        }

        public ICommand LoadTenantsCommand { get; }
        public ICommand SearchCommand { get; }
        public ICommand AddTenantCommand { get; }
        public ICommand EditTenantCommand { get; }
        public ICommand RemoveTenantCommand { get; }

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
                }
            }
        }

        private async Task LoadTenantsAsync()
        {
            try
            {
                var tenants = await _tenantService.GetAllTenantsAsync();
                Tenants.Clear();
                foreach (var tenant in tenants)
                {
                    Tenants.Add(tenant);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading tenants: {ex.Message}");
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
                System.Diagnostics.Debug.WriteLine($"Error searching tenants: {ex.Message}");
            }
        }

        private void ShowAddTenantDialog()
        {
            try
            {
                var viewModel = _serviceProvider.GetRequiredService<AddTenantViewModel>();
                var dialog = new MVVM.Views.AddTenantDialog(viewModel);
                dialog.Owner = System.Windows.Application.Current.MainWindow;
                if (dialog.ShowDialog() == true)
                {
                    LoadTenantsCommand.Execute(null);
                }
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
                dialog.Owner = System.Windows.Application.Current.MainWindow;
                if (dialog.ShowDialog() == true)
                {
                    LoadTenantsCommand.Execute(null);
                }
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

            var confirmed = _dialogService.ShowConfirmation(
                $"Are you sure you want to archive tenant '{SelectedTenant.FullName}'?\n\nThis will mark them as archived but keep their data.",
                "Archive Tenant");

            if (!confirmed) return;

            try
            {
                await _tenantService.ArchiveTenantAsync(SelectedTenant.TenantId);
                _dialogService.ShowMessage("Tenant archived successfully.", "Success");
                LoadTenantsCommand.Execute(null);
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error archiving tenant: {ex.Message}", "Error");
            }
        }
    }
}
