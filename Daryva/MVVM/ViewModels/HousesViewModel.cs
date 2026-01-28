using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Windows.Input;
using Avalonia.Threading;
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
            RemoveHouseCommand = new RelayCommand(async _ => await RemoveHouseAsync(), _ => SelectedHouse != null);
            ExportReportCommand = new RelayCommand(async _ => await ExportHouseReportAsync(), _ => SelectedHouse != null);
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
                    ((RelayCommand)ExportReportCommand).RaiseCanExecuteChanged();
                    System.Diagnostics.Debug.WriteLine($"SelectedHouse changed to: {value?.AddressLine1 ?? "null"}");
                }
            }
        }

        private async Task LoadHousesAsync()
        {
            try
            {
                var houses = await _houseService.GetAllHousesAsync();
                
                // Clear and reload on UI thread
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    Houses.Clear();
                    foreach (var house in houses)
                    {
                        if (!ShowActiveOnly || house.ActiveTenantCount > 0)
                        {
                            Houses.Add(house);
                        }
                    }
                });
                
                System.Diagnostics.Debug.WriteLine($"Loaded {Houses.Count} houses into the collection");
                OnPropertyChanged(nameof(Houses));
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error loading houses: {ex.Message}\n\nStack trace: {ex.StackTrace}", "Database Error");
                System.Diagnostics.Debug.WriteLine($"Error loading houses: {ex}");
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
                OnPropertyChanged(nameof(Houses));
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error searching houses: {ex.Message}", "Database Error");
                System.Diagnostics.Debug.WriteLine($"Error searching houses: {ex}");
            }
        }

        private void ShowAddHouseDialog()
        {
            var viewModel = _serviceProvider.GetRequiredService<AddHouseViewModel>();
            var dialog = new MVVM.Views.AddHouseDialog(viewModel);
            var mainWindow = Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop 
                ? desktop.MainWindow 
                : null;
            if (mainWindow != null)
            {
                dialog.WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterOwner;
                dialog.ShowDialog(mainWindow);
            }
            else
            {
                dialog.WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterScreen;
                dialog.Show();
            }
            LoadHousesCommand.Execute(null);
        }

        private async Task RemoveHouseAsync()
        {
            if (SelectedHouse == null) return;

            var selectedHouse = SelectedHouse; // Capture reference before deletion

            // Check if house has active tenants
            if (selectedHouse.ActiveTenantCount > 0)
            {
                _dialogService.ShowMessage(
                    $"Cannot delete house '{selectedHouse.AddressLine1}' because it has {selectedHouse.ActiveTenantCount} active tenant(s). Please end all tenancies first.",
                    "Cannot Delete House");
                return;
            }

            // Show confirmation dialog directly to avoid deadlock with DialogService
            var confirmed = await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var mainWindow = Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop 
                    ? desktop.MainWindow 
                    : null;

                if (mainWindow == null) return false;

                var tcs = new System.Threading.Tasks.TaskCompletionSource<bool>();

                var yesButton = new Avalonia.Controls.Button
                {
                    Content = "Yes",
                    Width = 80
                };
                var noButton = new Avalonia.Controls.Button
                {
                    Content = "No",
                    Width = 80
                };

                var msgBox = new Avalonia.Controls.Window
                {
                    Title = "Delete House",
                    Width = 400,
                    Height = 200,
                    WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterOwner,
                    CanResize = false,
                    ShowInTaskbar = false,
                    Content = new Avalonia.Controls.StackPanel
                    {
                        Margin = new Avalonia.Thickness(20),
                        Children =
                        {
                            new Avalonia.Controls.TextBlock
                            {
                                Text = $"Are you sure you want to delete house '{selectedHouse.AddressLine1}'?\n\nThis action cannot be undone.",
                                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                                Margin = new Avalonia.Thickness(0, 0, 0, 20)
                            },
                            new Avalonia.Controls.StackPanel
                            {
                                Orientation = Avalonia.Layout.Orientation.Horizontal,
                                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                                Spacing = 10,
                                Children =
                                {
                                    yesButton,
                                    noButton
                                }
                            }
                        }
                    }
                };

                yesButton.Click += (s, e) => 
                { 
                    tcs.TrySetResult(true); 
                    msgBox.Close(); 
                };
                noButton.Click += (s, e) => 
                { 
                    tcs.TrySetResult(false); 
                    msgBox.Close(); 
                };

                // Show dialog - this will block until closed
                await msgBox.ShowDialog(mainWindow);
                
                // If dialog closed without button click, return false
                if (!tcs.Task.IsCompleted)
                    tcs.TrySetResult(false);

                return await tcs.Task;
            });

            if (!confirmed) return;

            try
            {
                var houseId = selectedHouse.HouseId;
                
                // Run database operation on background thread to avoid blocking UI
                await Task.Run(async () =>
                {
                    await _houseService.DeleteHouseAsync(houseId).ConfigureAwait(false);
                }).ConfigureAwait(true); // Return to UI thread after completion
                
                // Update UI on UI thread
                // Remove from collection
                var houseToRemove = Houses.FirstOrDefault(h => h.HouseId == houseId);
                if (houseToRemove != null)
                {
                    Houses.Remove(houseToRemove);
                }
                
                // Clear selection
                SelectedHouse = null;
                
                // Show success message
                _dialogService.ShowMessage("House deleted successfully.", "Success");
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error deleting house: {ex.Message}", "Error");
                System.Diagnostics.Debug.WriteLine($"Error deleting house: {ex}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
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
