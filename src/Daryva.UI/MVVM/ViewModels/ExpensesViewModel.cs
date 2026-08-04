using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Threading;
using Daryva.MVVM.Commands;
using Daryva.MVVM.Models;
using Daryva.Services;
using Daryva.Services.Business;
using Daryva.Services.Data;
using Daryva.Services.Dialog;
using Daryva.Services.OrgContext;
using Microsoft.Extensions.DependencyInjection;

namespace Daryva.MVVM.ViewModels
{
    public class ExpensesViewModel : BaseViewModel
    {
        private readonly IExpenseService _expenseService;
        private readonly IHouseService _houseService;
        private readonly IDocumentService _documentService;
        private readonly IDialogService _dialogService;
        private readonly IServiceProvider _serviceProvider;
        private readonly ISettingsService _settingsService;
        private readonly IOrgContext _orgContext;

        private string _selectedTab = "Summary"; // "Summary" (first) or "List"
        private int _selectedTabIndex = 0;
        private string _dateRangeFilter = "All";
        private int? _selectedHouseId = 0; // 0 = All Houses
        private string _categoryFilter = "All";
        private string _searchTerm = string.Empty;
        private ExpenseRowViewModel? _selectedExpense;
        private ExpenseSummary? _summary;

        public ExpensesViewModel(
            IExpenseService expenseService,
            IHouseService houseService,
            IDocumentService documentService,
            IDialogService dialogService,
            IServiceProvider serviceProvider,
            ISettingsService settingsService,
            IOrgContext orgContext)
        {
            _expenseService = expenseService ?? throw new ArgumentNullException(nameof(expenseService));
            _houseService = houseService ?? throw new ArgumentNullException(nameof(houseService));
            _documentService = documentService ?? throw new ArgumentNullException(nameof(documentService));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _orgContext = orgContext ?? throw new ArgumentNullException(nameof(orgContext));

            Expenses = new ObservableCollection<ExpenseRowViewModel>();
            Houses = new ObservableCollection<House>();
            Summary = new ExpenseSummary();
            DateRangeFilterOptions = new ObservableCollection<string> { "This Month", "Last 3 Months", "This Year", "All" };
            CategoryFilterOptions = new ObservableCollection<string> { "All", "Repairs", "Bills", "CouncilTax", "Internet", "Furniture", "Cleaning", "Maintenance", "Other" };

            LoadExpensesCommand = new RelayCommand(async _ => await LoadExpensesAsync());
            LoadHousesCommand = new RelayCommand(async _ => await LoadHousesAsync());
            LoadSummaryCommand = new RelayCommand(async _ => await LoadSummaryAsync());
            AddExpenseCommand = new RelayCommand(_ => ShowAddExpenseDialog());
            EditExpenseCommand = new RelayCommand(parameter => 
            {
                if (parameter is ExpenseRowViewModel expense)
                {
                    SelectedExpense = expense;
                }
                ShowEditExpenseDialog();
            }, _ => true);
            DeleteExpenseCommand = new RelayCommand(async parameter => 
            {
                if (parameter is ExpenseRowViewModel expense)
                {
                    SelectedExpense = expense;
                }
                await DeleteExpenseAsync();
            }, _ => true);
            AttachReceiptCommand = new RelayCommand(async parameter => 
            {
                if (parameter is ExpenseRowViewModel expense)
                {
                    SelectedExpense = expense;
                }
                await AttachReceiptAsync();
            }, _ => true);
            ViewReceiptCommand = new RelayCommand(async parameter =>
            {
                if (parameter is ExpenseRowViewModel expense)
                    SelectedExpense = expense;
                await ViewReceiptAsync();
            }, parameter => (parameter is ExpenseRowViewModel r && r.HasReceipt) || (SelectedExpense != null && SelectedExpense.HasReceipt));
            ViewOrAddReceiptCommand = new RelayCommand(async parameter =>
            {
                if (parameter is ExpenseRowViewModel expense)
                {
                    SelectedExpense = expense;
                    if (expense.HasReceipt)
                        await ViewReceiptAsync();
                    else
                        await AttachReceiptAsync();
                }
            }, _ => true);
            ExportCsvCommand = new RelayCommand(async _ => await ExportCsvAsync());
            ClearFiltersCommand = new RelayCommand(_ =>
            {
                DateRangeFilter = "All";
                SelectedHouseId = 0;
                CategoryFilter = "All";
                SearchTerm = "";
            });

            _orgContext.CurrentOrgChanged += OnCurrentOrgChanged;

            // Load initial data asynchronously on UI thread
            _ = LoadInitialDataAsync();
        }

        private void OnCurrentOrgChanged(object? sender, CurrentOrgChangedEventArgs e)
        {
            Dispatcher.UIThread.Post(() => _ = LoadInitialDataAsync());
        }

        public ICommand LoadExpensesCommand { get; }
        public ICommand LoadHousesCommand { get; }
        public ICommand LoadSummaryCommand { get; }
        public ICommand AddExpenseCommand { get; }
        public ICommand EditExpenseCommand { get; }
        public ICommand DeleteExpenseCommand { get; }
        public ICommand AttachReceiptCommand { get; }
        public ICommand ViewReceiptCommand { get; }
        public ICommand ViewOrAddReceiptCommand { get; }
        public ICommand ExportCsvCommand { get; }
        public ICommand ClearFiltersCommand { get; }

        public ObservableCollection<ExpenseRowViewModel> Expenses { get; }
        public ObservableCollection<House> Houses { get; }
        public ObservableCollection<string> DateRangeFilterOptions { get; }
        public ObservableCollection<string> CategoryFilterOptions { get; }

        public string SelectedTab
        {
            get => _selectedTab;
            set
            {
                if (SetProperty(ref _selectedTab, value))
                {
                    SelectedTabIndex = value == "List" ? 1 : 0; // Summary=0, List=1
                    if (value == "Summary")
                    {
                        LoadSummaryCommand.Execute(null);
                    }
                }
            }
        }

        public int SelectedTabIndex
        {
            get => _selectedTabIndex;
            set
            {
                if (SetProperty(ref _selectedTabIndex, value))
                {
                    SelectedTab = value == 0 ? "Summary" : "List"; // 0=Summary, 1=List
                    if (value == 0)
                    {
                        LoadSummaryCommand.Execute(null);
                    }
                }
            }
        }

        public string DateRangeFilter
        {
            get => _dateRangeFilter;
            set
            {
                if (SetProperty(ref _dateRangeFilter, value))
                {
                    OnPropertyChanged(nameof(SubtitleText));
                    LoadExpensesCommand.Execute(null);
                    if (SelectedTab == "Summary")
                    {
                        LoadSummaryCommand.Execute(null);
                    }
                }
            }
        }

        public int? SelectedHouseId
        {
            get => _selectedHouseId;
            set
            {
                if (SetProperty(ref _selectedHouseId, value))
                {
                    LoadExpensesCommand.Execute(null);
                    if (SelectedTab == "Summary")
                    {
                        LoadSummaryCommand.Execute(null);
                    }
                }
            }
        }

        public string CategoryFilter
        {
            get => _categoryFilter;
            set
            {
                if (SetProperty(ref _categoryFilter, value))
                {
                    OnPropertyChanged(nameof(SubtitleText));
                    LoadExpensesCommand.Execute(null);
                    if (SelectedTab == "Summary")
                        LoadSummaryCommand.Execute(null);
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
                    LoadExpensesCommand.Execute(null);
                    if (SelectedTab == "Summary")
                        LoadSummaryCommand.Execute(null);
                }
            }
        }

        public ExpenseRowViewModel? SelectedExpense
        {
            get => _selectedExpense;
            set
            {
                if (SetProperty(ref _selectedExpense, value))
                {
                    ((RelayCommand)EditExpenseCommand).RaiseCanExecuteChanged();
                    ((RelayCommand)DeleteExpenseCommand).RaiseCanExecuteChanged();
                    ((RelayCommand)AttachReceiptCommand).RaiseCanExecuteChanged();
                    ((RelayCommand)ViewReceiptCommand).RaiseCanExecuteChanged();
                }
            }
        }

        public ExpenseSummary? Summary
        {
            get => _summary;
            set => SetProperty(ref _summary, value);
        }

        /// <summary>For command bar subtitle: e.g. "This Month • All Houses • All"</summary>
        public string SubtitleText =>
            $"{DateRangeFilter} • {(SelectedHouseId == null || SelectedHouseId == 0 ? "All Houses" : Houses.FirstOrDefault(h => h.HouseId == SelectedHouseId)?.AddressLine1 ?? "House")} • {CategoryFilter}";

        private async Task LoadInitialDataAsync()
        {
            await LoadHousesAsync();
            await LoadExpensesAsync();
        }

        private async Task LoadHousesAsync()
        {
            try
            {
                var houses = await _houseService.GetAllHousesAsync();
                
                // Ensure UI updates happen on the UI thread
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    Houses.Clear();
                    Houses.Add(new House { HouseId = 0, AddressLine1 = "All Houses" });
                    foreach (var house in houses)
                    {
                        Houses.Add(house);
                    }
                    OnPropertyChanged(nameof(SubtitleText));
                });
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error loading houses: {ex.Message}", "Error");
            }
        }

        private async Task LoadExpensesAsync()
        {
            try
            {
                DateTime? startDate = null;
                DateTime? endDate = null;

                // Calculate date range based on filter
                var now = DateTime.Now;
                switch (DateRangeFilter)
                {
                    case "This Month":
                        startDate = new DateTime(now.Year, now.Month, 1);
                        endDate = startDate.Value.AddMonths(1).AddDays(-1);
                        break;
                    case "Last 3 Months":
                        startDate = now.AddMonths(-3);
                        endDate = now;
                        break;
                    case "This Year":
                        startDate = new DateTime(now.Year, 1, 1);
                        endDate = new DateTime(now.Year, 12, 31);
                        break;
                    case "All":
                        // No date filter
                        break;
                }

                int? houseIdFilter = (SelectedHouseId == null || SelectedHouseId == 0) ? null : SelectedHouseId;
                string? categoryFilter = CategoryFilter == "All" ? null : CategoryFilter;
                string? searchFilter = string.IsNullOrWhiteSpace(SearchTerm) ? null : SearchTerm;

                var expenses = await _expenseService.GetExpensesAsync(houseIdFilter, startDate, endDate, categoryFilter, searchFilter);
                var dateFormat = await _settingsService.GetSettingAsync("DateFormat", "dd/MM/yyyy") ?? "dd/MM/yyyy";
                DateTimeFormatProvider.DateFormat = dateFormat;

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    Expenses.Clear();
                    foreach (var expense in expenses)
                    {
                        var houseAddress = !string.IsNullOrEmpty(expense.HouseAddress) ? expense.HouseAddress : 
                                         expense.House != null ? $"{expense.House.AddressLine1}, {expense.House.City}" : "Unknown";
                        var row = new ExpenseRowViewModel
                        {
                            ExpenseId = expense.HouseExpenseId,
                            DateIncurred = expense.DateIncurred,
                            HouseAddress = houseAddress,
                            Category = expense.Category,
                            Vendor = expense.Vendor ?? "",
                            Description = expense.Notes ?? "",
                            Amount = expense.Amount,
                            HasReceipt = expense.ReceiptDocumentId.HasValue,
                            ReceiptDocumentId = expense.ReceiptDocumentId,
                            HouseId = expense.HouseId
                        };
                        row.DateIncurredDisplay = DateTimeFormatProvider.FormatDate(expense.DateIncurred);
                        Expenses.Add(row);
                    }
                });
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error loading expenses: {ex.Message}", "Error");
            }
        }

        private async Task LoadSummaryAsync()
        {
            try
            {
                DateTime? startDate = null;
                DateTime? endDate = null;

                // Calculate date range based on filter
                var now = DateTime.Now;
                switch (DateRangeFilter)
                {
                    case "This Month":
                        startDate = new DateTime(now.Year, now.Month, 1);
                        endDate = startDate.Value.AddMonths(1).AddDays(-1);
                        break;
                    case "Last 3 Months":
                        startDate = now.AddMonths(-3);
                        endDate = now;
                        break;
                    case "This Year":
                        startDate = new DateTime(now.Year, 1, 1);
                        endDate = new DateTime(now.Year, 12, 31);
                        break;
                    case "All":
                        // No date filter
                        break;
                }

                int? houseIdFilter = (SelectedHouseId == null || SelectedHouseId == 0) ? null : SelectedHouseId;
                string? categoryFilter = CategoryFilter == "All" ? null : CategoryFilter;
                string? searchFilter = string.IsNullOrWhiteSpace(SearchTerm) ? null : SearchTerm;

                var summary = await _expenseService.GetExpenseSummaryAsync(houseIdFilter, startDate, endDate, categoryFilter, searchFilter);
                Summary = summary;
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error loading summary: {ex.Message}", "Error");
            }
        }

        private async void ShowAddExpenseDialog()
        {
            try
            {
                var viewModel = _serviceProvider.GetRequiredService<AddEditExpenseViewModel>();
                viewModel.IsEditMode = false;
                var dialog = new MVVM.Views.AddEditExpenseDialog(viewModel);
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
                    dialog.Closed += async (_, _) =>
                    {
                        await LoadExpensesAsync();
                        if (SelectedTab == "Summary") await LoadSummaryAsync();
                    };
                    dialog.Show();
                    return;
                }
                await LoadExpensesAsync();
                if (SelectedTab == "Summary")
                {
                    await LoadSummaryAsync();
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error opening add expense dialog: {ex.Message}", "Error");
            }
        }

    private async void ShowEditExpenseDialog()
        {
            if (SelectedExpense == null) return;

            try
            {
                var viewModel = _serviceProvider.GetRequiredService<AddEditExpenseViewModel>();
                viewModel.IsEditMode = true;
                await viewModel.LoadExpense(SelectedExpense.ExpenseId);
                var dialog = new MVVM.Views.AddEditExpenseDialog(viewModel);
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
                return;
            }
                await LoadExpensesAsync();
                if (SelectedTab == "Summary")
                {
                    await LoadSummaryAsync();
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error opening edit expense dialog: {ex.Message}", "Error");
            }
        }

        private async Task DeleteExpenseAsync()
        {
            if (SelectedExpense == null) return;

            var requireConfirm = await _settingsService.GetSettingAsync<bool>("ConfirmDestructiveActions", true) ?? true;
            var confirmed = !requireConfirm || await _dialogService.ShowConfirmationAsync(
                $"Are you sure you want to delete this expense of £{SelectedExpense.Amount:N2}?\n\nThis action cannot be undone.",
                "Confirm Delete");

            if (!confirmed) return;

            try
            {
                await _expenseService.DeleteExpenseAsync(SelectedExpense.ExpenseId);
                _dialogService.ShowMessage("Expense deleted successfully.", "Success");
                LoadExpensesCommand.Execute(null);
                if (SelectedTab == "Summary")
                {
                    LoadSummaryCommand.Execute(null);
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error deleting expense: {ex.Message}", "Error");
            }
        }

        private async Task AttachReceiptAsync()
        {
            if (SelectedExpense == null) return;

            try
            {
                var filePath = await _dialogService.ShowOpenFileDialogAsync("Image Files|*.jpg;*.jpeg;*.png;*.pdf|All Files|*.*", "Select Receipt");
                if (string.IsNullOrWhiteSpace(filePath))
                    return;

                var fileBytes = await File.ReadAllBytesAsync(filePath);
                var fileName = Path.GetFileName(filePath);

                // Create document linked to the house
                var document = new Document
                {
                    HouseId = SelectedExpense.HouseId,
                    Type = "Other",
                    FileName = fileName,
                    FileMimeType = System.IO.Path.GetExtension(fileName),
                    Source = "Uploaded"
                };

                // Upload document and get the document ID
                var uploadedDocument = await _documentService.UploadDocumentAsync(document, fileBytes);

                // Update expense with receipt document ID
                var expense = await _expenseService.GetExpenseByIdAsync(SelectedExpense.ExpenseId);
                if (expense != null)
                {
                    expense.ReceiptDocumentId = uploadedDocument.DocumentId;
                    await _expenseService.UpdateExpenseAsync(expense);
                    _dialogService.ShowMessage("Receipt attached successfully.", "Success");
                    LoadExpensesCommand.Execute(null);
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error attaching receipt: {ex.Message}", "Error");
            }
        }

        private async Task ViewReceiptAsync()
        {
            if (SelectedExpense == null || !SelectedExpense.HasReceipt || !SelectedExpense.ReceiptDocumentId.HasValue)
                return;

            try
            {
                var fileBytes = await _documentService.GetDocumentFileBytesAsync(SelectedExpense.ReceiptDocumentId.Value);
                if (fileBytes == null || fileBytes.Length == 0)
                {
                    _dialogService.ShowMessage("Receipt file not found.", "Error");
                    return;
                }

                // Save to temp file and open
                var tempPath = Path.Combine(Path.GetTempPath(), $"receipt_{SelectedExpense.ReceiptDocumentId}_{Guid.NewGuid()}.pdf");
                await File.WriteAllBytesAsync(tempPath, fileBytes);

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = tempPath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error viewing receipt: {ex.Message}", "Error");
            }
        }

        private async Task ExportCsvAsync()
        {
            try
            {
                DateTime? startDate = null;
                DateTime? endDate = null;

                // Calculate date range based on filter
                var now = DateTime.Now;
                switch (DateRangeFilter)
                {
                    case "This Month":
                        startDate = new DateTime(now.Year, now.Month, 1);
                        endDate = startDate.Value.AddMonths(1).AddDays(-1);
                        break;
                    case "Last 3 Months":
                        startDate = now.AddMonths(-3);
                        endDate = now;
                        break;
                    case "This Year":
                        startDate = new DateTime(now.Year, 1, 1);
                        endDate = new DateTime(now.Year, 12, 31);
                        break;
                    case "All":
                        // No date filter
                        break;
                }

                int? houseIdFilter = (SelectedHouseId == null || SelectedHouseId == 0) ? null : SelectedHouseId;
                string? categoryFilter = CategoryFilter == "All" ? null : CategoryFilter;

                var csvContent = await _expenseService.ExportExpensesToCsvAsync(houseIdFilter, startDate, endDate, categoryFilter);

                var defaultFileName = $"expenses_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                var filePath = _dialogService.ShowSaveFileDialog(defaultFileName, "CSV Files|*.csv|All Files|*.*", "Export Expenses");

                if (!string.IsNullOrWhiteSpace(filePath))
                {
                    await File.WriteAllTextAsync(filePath, csvContent);
                    _dialogService.ShowMessage($"Expenses exported successfully to:\n{filePath}", "Export Successful");
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error exporting expenses: {ex.Message}", "Error");
            }
        }
    }
}
