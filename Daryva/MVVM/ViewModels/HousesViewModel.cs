using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading;
using System.Windows.Input;
using Daryva.MVVM.Commands;
using Daryva.MVVM.Models;
using Daryva.Services.Business;
using Daryva.Services.Dialog;
using Microsoft.Extensions.DependencyInjection;

namespace Daryva.MVVM.ViewModels
{
    public class HousesViewModel : BaseViewModel
    {
        private readonly IHouseService _houseService;
        private readonly IDialogService _dialogService;
        private readonly IServiceProvider _serviceProvider;
        private readonly IHouseReportExportService _houseReportExportService;
        private string _searchTerm = string.Empty;
        private bool _showActiveOnly = false;
        private House? _selectedHouse;

        public HousesViewModel(IHouseService houseService, IDialogService dialogService, IServiceProvider serviceProvider, IHouseReportExportService houseReportExportService)
        {
            _houseService = houseService;
            _dialogService = dialogService;
            _serviceProvider = serviceProvider;
            _houseReportExportService = houseReportExportService ?? throw new ArgumentNullException(nameof(houseReportExportService));
            Houses = new ObservableCollection<House>();

            LoadHousesCommand = new RelayCommand(async _ => await LoadHousesAsync());
            SearchCommand = new RelayCommand(async _ => await SearchHousesAsync());
            AddHouseCommand = new RelayCommand(_ => ShowAddHouseDialog());
            RemoveHouseCommand = new RelayCommand(_ => RemoveHouseAsync(), _ => SelectedHouse != null);
            ExportReportCommand = new RelayCommand(async _ => await ExportHouseReportAsync(), _ => SelectedHouse != null);

            LoadHousesCommand.Execute(null);
        }

        public ICommand LoadHousesCommand { get; }
        public ICommand SearchCommand { get; }
        public ICommand AddHouseCommand { get; }
        public ICommand RemoveHouseCommand { get; }
        public ICommand ExportReportCommand { get; }

        public ObservableCollection<House> Houses { get; }

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

        public bool ShowActiveOnly
        {
            get => _showActiveOnly;
            set
            {
                if (SetProperty(ref _showActiveOnly, value))
                {
                    LoadHousesCommand.Execute(null);
                }
            }
        }

        public House? SelectedHouse
        {
            get => _selectedHouse;
            set
            {
                if (SetProperty(ref _selectedHouse, value))
                {
                    ((RelayCommand)RemoveHouseCommand).RaiseCanExecuteChanged();
                }
            }
        }

        private async Task LoadHousesAsync()
        {
            try
            {
                var houses = await _houseService.GetAllHousesAsync();
                Houses.Clear();
                foreach (var house in houses)
                {
                    if (!ShowActiveOnly || house.ActiveTenantCount > 0)
                    {
                        Houses.Add(house);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading houses: {ex.Message}");
            }
        }

        private async Task SearchHousesAsync()
        {
            if (string.IsNullOrWhiteSpace(SearchTerm))
            {
                await LoadHousesAsync();
                return;
            }

            try
            {
                var houses = await _houseService.SearchHousesAsync(SearchTerm);
                Houses.Clear();
                foreach (var house in houses)
                {
                    Houses.Add(house);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error searching houses: {ex.Message}");
            }
        }

        private void ShowAddHouseDialog()
        {
            var viewModel = _serviceProvider.GetRequiredService<AddHouseViewModel>();
            var dialog = new MVVM.Views.AddHouseDialog(viewModel);
            dialog.Owner = System.Windows.Application.Current.MainWindow;
            if (dialog.ShowDialog() == true)
            {
                LoadHousesCommand.Execute(null);
            }
        }

        private async void RemoveHouseAsync()
        {
            if (SelectedHouse == null) return;

            // Check if house has active tenants
            if (SelectedHouse.ActiveTenantCount > 0)
            {
                _dialogService.ShowMessage(
                    $"Cannot delete house '{SelectedHouse.AddressLine1}' because it has {SelectedHouse.ActiveTenantCount} active tenant(s). Please end all tenancies first.",
                    "Cannot Delete House");
                return;
            }

            var confirmed = _dialogService.ShowConfirmation(
                $"Are you sure you want to delete house '{SelectedHouse.AddressLine1}'?\n\nThis action cannot be undone.",
                "Delete House");

            if (!confirmed) return;

            try
            {
                await _houseService.DeleteHouseAsync(SelectedHouse.HouseId);
                _dialogService.ShowMessage("House deleted successfully.", "Success");
                LoadHousesCommand.Execute(null);
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error deleting house: {ex.Message}", "Error");
            }
        }

        private async Task ExportHouseReportAsync()
        {
            if (SelectedHouse == null) return;

            try
            {
                var houseName = SelectedHouse.Postcode?.Replace(" ", "") ?? SelectedHouse.AddressLine1.Replace(" ", "");
                var defaultFile = $"Daryva_HouseReport_{houseName}_{DateTime.Now:yyyy-MM-dd}.xlsx";
                var path = _dialogService.ShowSaveFileDialog(defaultFile, "Excel Files|*.xlsx", "Save House Report");
                
                if (string.IsNullOrWhiteSpace(path))
                    return;

                // Optional: Ask for date range (for now, use all-time)
                var range = new DateRange
                {
                    FromDate = null, // All-time
                    ToDate = null
                };

                await _houseReportExportService.ExportHouseReportAsync(SelectedHouse.HouseId, range, path, CancellationToken.None);
                _dialogService.ShowMessage($"House report exported successfully to:\n{path}", "Export Complete");
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error exporting house report: {ex.Message}", "Error");
            }
        }
    }
}
