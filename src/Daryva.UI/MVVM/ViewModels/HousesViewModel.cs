using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Input;
using Avalonia.Threading;
using Daryva.MVVM.Commands;
using Daryva.MVVM.Models;
using Daryva.Services.Business;
using Daryva.Services.Dialog;
using Daryva.Services.Navigation;
using Daryva.Services.OrgContext;
using Microsoft.Extensions.DependencyInjection;

namespace Daryva.MVVM.ViewModels
{
    public class HousesViewModel : BaseViewModel
    {
        private readonly IHouseService _houseService;
        private readonly IDialogService _dialogService;
        private readonly IServiceProvider _serviceProvider;
        private readonly IHouseReportExportService _houseReportExportService;
        private readonly ISettingsService _settingsService;
        private readonly INavigationService _navigationService;
        private readonly IOrgContext _orgContext;
        private string _searchTerm = string.Empty;
        private bool _showActiveOnly = false;
        private bool _showArchivedOnly = false;
        private House? _selectedHouse;
        private bool _isLoading;

        public HousesViewModel(
            IHouseService houseService,
            IDialogService dialogService,
            IServiceProvider serviceProvider,
            IHouseReportExportService houseReportExportService,
            ISettingsService settingsService,
            INavigationService navigationService)
        {
            _houseService = houseService;
            _dialogService = dialogService;
            _serviceProvider = serviceProvider;
            _houseReportExportService = houseReportExportService ?? throw new ArgumentNullException(nameof(houseReportExportService));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
            Houses = new ObservableCollection<House>();
            Houses.CollectionChanged += OnHousesCollectionChanged;

            LoadHousesCommand = new RelayCommand(async _ => await LoadHousesAsync());
            SearchCommand = new RelayCommand(async _ => await SearchHousesAsync());
            AddHouseCommand = new RelayCommand(async _ => await ShowAddHouseDialogAsync());
            RemoveHouseCommand = new RelayCommand(async _ => await RemoveHouseAsync(), _ => SelectedHouse != null);
            ExportReportCommand = new RelayCommand(async _ => await ExportHouseReportAsync(), _ => SelectedHouse != null);
            ClearSearchCommand = new RelayCommand(_ => SearchTerm = string.Empty, _ => !string.IsNullOrWhiteSpace(SearchTerm));

            // Row-scoped actions (parameter = House for that row)
            OpenHouseCommand = new RelayCommand<House>(house => _ = OpenHouseAsync(house), house => house != null);
            EditHouseCommand = new RelayCommand<House>(house => _ = EditHouseAsync(house), house => house != null);
            AddTenantForHouseCommand = new RelayCommand<House>(house => _ = AddTenantForHouseAsync(house), house => house != null);
            RecordExpenseForHouseCommand = new RelayCommand<House>(house => _ = RecordExpenseForHouseAsync(house), house => house != null);
            UploadDocumentForHouseCommand = new RelayCommand<House>(house => _ = UploadDocumentForHouseAsync(house), house => house != null);
            ExportHouseReportForHouseCommand = new RelayCommand<House>(house => _ = ExportHouseReportForHouseAsync(house), house => house != null);
            ArchiveHouseCommand = new RelayCommand<House>(house => _ = ArchiveHouseAsync(house), house => house != null);
            DeleteHouseCommand = new RelayCommand<House>(house => _ = DeleteHouseAsync(house), house => house != null && CanDeleteHouse(house));

            ClearSelectionCommand = new RelayCommand(_ => SelectedHouse = null, _ => SelectedHouse != null);
            OpenSelectedHouseCommand = new RelayCommand(_ => OpenHouseAsync(SelectedHouse), _ => SelectedHouse != null);

            LoadHousesCommand.Execute(null);
        }

        private void OnCurrentOrgChanged(object? sender, CurrentOrgChangedEventArgs e)
        {
            Dispatcher.UIThread.Post(() => LoadHousesCommand.Execute(null));
        }

        public ICommand LoadHousesCommand { get; }
        public ICommand SearchCommand { get; }
        public ICommand AddHouseCommand { get; }
        public ICommand RemoveHouseCommand { get; }
        public ICommand ExportReportCommand { get; }
        public ICommand ClearSearchCommand { get; }

        /// <summary>Row action: open house details (Phase 4 will navigate to HouseDetailsView).</summary>
        public ICommand OpenHouseCommand { get; }
        /// <summary>Row action: edit house (opens AddHouse dialog).</summary>
        public ICommand EditHouseCommand { get; }
        /// <summary>Row action: add tenant for this house.</summary>
        public ICommand AddTenantForHouseCommand { get; }
        /// <summary>Row action: record expense for this house.</summary>
        public ICommand RecordExpenseForHouseCommand { get; }
        /// <summary>Row action: upload document for this house.</summary>
        public ICommand UploadDocumentForHouseCommand { get; }
        /// <summary>Row action: export house report for this house.</summary>
        public ICommand ExportHouseReportForHouseCommand { get; }
        /// <summary>Row action: archive house.</summary>
        public ICommand ArchiveHouseCommand { get; }
        /// <summary>Row action: delete house (only when safe; otherwise use Archive).</summary>
        public ICommand DeleteHouseCommand { get; }
        /// <summary>Clear the selected house (e.g. Esc key).</summary>
        public ICommand ClearSelectionCommand { get; }
        /// <summary>Open the selected house (e.g. Enter key or double-click).</summary>
        public ICommand OpenSelectedHouseCommand { get; }

        public ObservableCollection<House> Houses { get; }

        public bool IsLoading
        {
            get => _isLoading;
            private set
            {
                if (SetProperty(ref _isLoading, value))
                    OnPropertyChanged(nameof(IsSearchNoResults));
            }
        }

        public int HouseCount => Houses.Count;
        public int TotalRooms => Houses.Sum(h => h.TotalRooms);
        public int TotalActiveTenants => Houses.Sum(h => h.ActiveTenantCount);
        public decimal TotalMonthlyRent => Houses.Sum(h => h.TotalMonthlyRent);
        public bool HasHouses => Houses.Count > 0;
        /// <summary>Header subtitle: e.g. "3 houses • £2,400/mo • 5 tenants".</summary>
        public string SubtitleText => $"{HouseCount} houses • £{TotalMonthlyRent:N0}/mo • {TotalActiveTenants} tenants";
        /// <summary>True when the list is empty due to search/filter (show "No houses match your search").</summary>
        public bool IsSearchNoResults => !HasHouses && !string.IsNullOrWhiteSpace(SearchTerm) && !IsLoading;

        public string SearchTerm
        {
            get => _searchTerm;
            set
            {
                if (SetProperty(ref _searchTerm, value))
                {
                    ((RelayCommand)ClearSearchCommand).RaiseCanExecuteChanged();
                    OnPropertyChanged(nameof(IsSearchNoResults));
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

        /// <summary>When true, archived houses are included in the list.</summary>
        public bool ShowArchivedOnly
        {
            get => _showArchivedOnly;
            set
            {
                if (SetProperty(ref _showArchivedOnly, value))
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
                    ((RelayCommand)ClearSelectionCommand).RaiseCanExecuteChanged();
                    ((RelayCommand)OpenSelectedHouseCommand).RaiseCanExecuteChanged();
                }
            }
        }

        private async Task LoadHousesAsync()
        {
            try
            {
                IsLoading = true;
                var houses = await _houseService.GetAllHousesAsync(includeArchived: ShowArchivedOnly);
                
                // Clear and reload on UI thread
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    Houses.Clear();
                    foreach (var house in houses)
                    {
                        if (ShowArchivedOnly && !house.IsArchived)
                            continue;
                        if (!ShowArchivedOnly && house.IsArchived)
                            continue;
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
            finally
            {
                IsLoading = false;
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
                IsLoading = true;
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
            finally
            {
                IsLoading = false;
            }
        }

        private void OnHousesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(HouseCount));
            OnPropertyChanged(nameof(TotalRooms));
            OnPropertyChanged(nameof(TotalActiveTenants));
            OnPropertyChanged(nameof(TotalMonthlyRent));
            OnPropertyChanged(nameof(HasHouses));
            OnPropertyChanged(nameof(IsSearchNoResults));
            OnPropertyChanged(nameof(SubtitleText));
        }

        private async Task ShowAddHouseDialogAsync()
        {
            var viewModel = _serviceProvider.GetRequiredService<AddHouseViewModel>();
            var dialog = new MVVM.Views.AddHouseDialog(viewModel);
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
            await LoadHousesAsync();
        }

        private async Task RemoveHouseAsync()
        {
            if (SelectedHouse == null) return;

            var selectedHouse = SelectedHouse; // Capture reference before deletion

            // Check if house has any ACTIVE tenancies (ended tenancies for archived tenants are OK)
            try
            {
                var hasActiveTenancies = await _houseService.HasTenanciesAsync(selectedHouse.HouseId);
                if (hasActiveTenancies)
                {
                    _dialogService.ShowMessage(
                        $"Cannot delete house '{selectedHouse.AddressLine1}' because it has active tenancies.\n\nPlease end all active tenancies first.\n\nNote: Ended tenancies for archived tenants do not prevent deletion.",
                        "Cannot Delete House");
                    return;
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage(
                    $"Error checking tenancies: {ex.Message}",
                    "Error");
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
            catch (InvalidOperationException ex)
            {
                // User-friendly message for active tenancies
                _dialogService.ShowMessage(ex.Message, "Cannot Delete House");
                System.Diagnostics.Debug.WriteLine($"Error deleting house: {ex}");
            }
            catch (Exception ex)
            {
                // Check if it's a foreign key constraint error (shouldn't happen now, but just in case)
                var errorMessage = ex.Message;
                if (errorMessage.Contains("REFERENCE constraint") || errorMessage.Contains("FK_"))
                {
                    _dialogService.ShowMessage(
                        $"Cannot delete house '{selectedHouse.AddressLine1}' because it still has active tenancies.\n\nPlease end all active tenancies first.\n\nNote: If all tenants are archived and tenancies are ended, deletion should work.",
                        "Cannot Delete House");
                }
                else
                {
                    _dialogService.ShowMessage($"Error deleting house: {ex.Message}", "Error");
                }
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

                // Use document storage path from settings; fallback to a project-relative HouseReports folder
                var documentPath = await _settingsService.GetSettingAsync("DocumentStoragePath", string.Empty) ?? string.Empty;

                string baseFolder;
                if (!string.IsNullOrWhiteSpace(documentPath))
                {
                    baseFolder = Path.Combine(documentPath, "HouseReports");
                }
                else
                {
                    var projectRoot = AppContext.BaseDirectory;
                    baseFolder = Path.Combine(projectRoot, "HouseReports");
                }

                if (!Directory.Exists(baseFolder))
                {
                    Directory.CreateDirectory(baseFolder);
                }

                var path = Path.Combine(baseFolder, defaultFile);

                // Optional: Ask for date range (for now, use all-time)
                var range = new DateRange
                {
                    FromDate = null, // All-time
                    ToDate = null
                };

                string? exportedPath = null;

                await _dialogService.RunWithProgressAsync(
                    "Exporting House Report",
                    $"Generating Excel report for {SelectedHouse.AddressLine1}. Please wait...",
                    async () =>
                    {
                        exportedPath = await _houseReportExportService.ExportHouseReportAsync(
                            SelectedHouse.HouseId,
                            range,
                            path,
                            CancellationToken.None);
                    });

                if (!string.IsNullOrWhiteSpace(exportedPath))
                {
                    _dialogService.ShowMessage($"House report exported successfully to:\n{exportedPath}", "Export Complete");
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error exporting house report: {ex.Message}", "Error");
            }
        }

        private async Task OpenHouseAsync(House? house)
        {
            if (house == null) return;
            SelectedHouse = house;
            // Phase 4: navigate to HouseDetailsView with house.HouseId
        }

        private async Task EditHouseAsync(House? house)
        {
            if (house == null) return;
            var viewModel = _serviceProvider.GetRequiredService<AddHouseViewModel>();
            viewModel.LoadForEdit(house);
            var dialog = new MVVM.Views.AddHouseDialog(viewModel);
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
            await LoadHousesAsync();
        }

        private async Task AddTenantForHouseAsync(House? house)
        {
            if (house == null) return;
            // Placeholder: open Add Tenant flow for house.HouseId (Phase 3)
            await Task.CompletedTask;
        }

        private async Task RecordExpenseForHouseAsync(House? house)
        {
            if (house == null) return;
            // Placeholder: open Record Expense for house (Phase 3)
            await Task.CompletedTask;
        }

        private async Task UploadDocumentForHouseAsync(House? house)
        {
            if (house == null) return;
            // Placeholder: open Upload Document for house (Phase 3)
            await Task.CompletedTask;
        }

        private async Task ExportHouseReportForHouseAsync(House? house)
        {
            if (house == null) return;
            try
            {
                var houseName = house.Postcode?.Replace(" ", "") ?? house.AddressLine1.Replace(" ", "");
                var defaultFile = $"Daryva_HouseReport_{houseName}_{DateTime.Now:yyyy-MM-dd}.xlsx";
                var documentPath = await _settingsService.GetSettingAsync("DocumentStoragePath", string.Empty) ?? string.Empty;
                string baseFolder = !string.IsNullOrWhiteSpace(documentPath)
                    ? Path.Combine(documentPath, "HouseReports")
                    : Path.Combine(AppContext.BaseDirectory, "HouseReports");
                if (!Directory.Exists(baseFolder)) Directory.CreateDirectory(baseFolder);
                var path = Path.Combine(baseFolder, defaultFile);
                var range = new DateRange { FromDate = null, ToDate = null };
                string? exportedPath = null;
                await _dialogService.RunWithProgressAsync(
                    "Exporting House Report",
                    $"Generating Excel report for {house.AddressLine1}. Please wait...",
                    async () =>
                    {
                        exportedPath = await _houseReportExportService.ExportHouseReportAsync(
                            house.HouseId, range, path, CancellationToken.None);
                    });
                if (!string.IsNullOrWhiteSpace(exportedPath))
                    _dialogService.ShowMessage($"House report exported successfully to:\n{exportedPath}", "Export Complete");
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error exporting house report: {ex.Message}", "Error");
            }
        }

        private async Task ArchiveHouseAsync(House? house)
        {
            if (house == null) return;
            try
            {
                var updated = await _houseService.ArchiveHouseAsync(house.HouseId);
                if (updated != null)
                {
                    await LoadHousesAsync();
                    _dialogService.ShowMessage($"House '{house.AddressLine1}' has been archived. Use \"Show archived\" to see it.", "Archived");
                }
                else
                {
                    _dialogService.ShowMessage("Archive is not supported by the current data source.", "Archive");
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error archiving house: {ex.Message}", "Error");
            }
        }

        private bool CanDeleteHouse(House? house)
        {
            if (house == null) return false;
            // Safe to delete only when no active tenants (payments/expenses/documents could be added when API exposes counts)
            return house.ActiveTenantCount == 0;
        }

        private async Task DeleteHouseAsync(House? house)
        {
            if (house == null) return;
            SelectedHouse = house;
            await RemoveHouseAsync();
        }
    }
}
