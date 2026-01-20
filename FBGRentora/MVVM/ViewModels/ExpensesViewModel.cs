using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using FBGRentora.MVVM.Commands;
using FBGRentora.MVVM.Models;
using FBGRentora.Services.Business;
using FBGRentora.Services.Data;
using FBGRentora.Services.Dialog;
using Microsoft.Extensions.DependencyInjection;

namespace FBGRentora.MVVM.ViewModels
{
    public class ExpensesViewModel : BaseViewModel
    {
        private readonly IExpenseService _expenseService;
        private readonly IHouseService _houseService;
        private readonly IDocumentService _documentService;
        private readonly IDialogService _dialogService;
        private readonly IServiceProvider _serviceProvider;

        private string _selectedTab = "List"; // "List" or "Summary"
        private string _dateRangeFilter = "This Month";
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
            IServiceProvider serviceProvider)
        {
            _expenseService = expenseService ?? throw new ArgumentNullException(nameof(expenseService));
            _houseService = houseService ?? throw new ArgumentNullException(nameof(houseService));
            _documentService = documentService ?? throw new ArgumentNullException(nameof(documentService));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

            Expenses = new ObservableCollection<ExpenseRowViewModel>();
            Houses = new ObservableCollection<House>();
            Summary = new ExpenseSummary();

            LoadExpensesCommand = new RelayCommand(async _ => await LoadExpensesAsync());
            LoadHousesCommand = new RelayCommand(async _ => await LoadHousesAsync());
            LoadSummaryCommand = new RelayCommand(async _ => await LoadSummaryAsync());
            AddExpenseCommand = new RelayCommand(_ => ShowAddExpenseDialog());
            EditExpenseCommand = new RelayCommand(_ => ShowEditExpenseDialog(), _ => SelectedExpense != null);
            DeleteExpenseCommand = new RelayCommand(async _ => await DeleteExpenseAsync(), _ => SelectedExpense != null);
            AttachReceiptCommand = new RelayCommand(async _ => await AttachReceiptAsync(), _ => SelectedExpense != null);
            ViewReceiptCommand = new RelayCommand(async _ => await ViewReceiptAsync(), _ => SelectedExpense != null && SelectedExpense.HasReceipt);
            ExportCsvCommand = new RelayCommand(async _ => await ExportCsvAsync());

            // Load initial data asynchronously on UI thread
            _ = LoadInitialDataAsync();
        }

        public ICommand LoadExpensesCommand { get; }
        public ICommand LoadHousesCommand { get; }
        public ICommand LoadSummaryCommand { get; }
        public ICommand AddExpenseCommand { get; }
        public ICommand EditExpenseCommand { get; }
        public ICommand DeleteExpenseCommand { get; }
        public ICommand AttachReceiptCommand { get; }
        public ICommand ViewReceiptCommand { get; }
        public ICommand ExportCsvCommand { get; }

        public ObservableCollection<ExpenseRowViewModel> Expenses { get; }
        public ObservableCollection<House> Houses { get; }

        public string SelectedTab
        {
            get => _selectedTab;
            set
            {
                if (SetProperty(ref _selectedTab, value))
                {
                    if (value == "Summary")
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
                    LoadExpensesCommand.Execute(null);
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
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    Houses.Clear();
                    Houses.Add(new House { HouseId = 0, AddressLine1 = "All Houses" });
                    foreach (var house in houses)
                    {
                        Houses.Add(house);
                    }
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

                // Ensure UI updates happen on the UI thread
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    Expenses.Clear();
                    foreach (var expense in expenses)
                    {
                        var houseAddress = expense.House != null ? $"{expense.House.AddressLine1}, {expense.House.City}" : "Unknown";
                        Expenses.Add(new ExpenseRowViewModel
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
                        });
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

                var summary = await _expenseService.GetExpenseSummaryAsync(houseIdFilter, startDate, endDate);
                Summary = summary;
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error loading summary: {ex.Message}", "Error");
            }
        }

        private void ShowAddExpenseDialog()
        {
            try
            {
                var viewModel = _serviceProvider.GetRequiredService<AddEditExpenseViewModel>();
                viewModel.IsEditMode = false;
                var dialog = new MVVM.Views.AddEditExpenseDialog(viewModel);
                dialog.Owner = System.Windows.Application.Current.MainWindow;
                if (dialog.ShowDialog() == true)
                {
                    LoadExpensesCommand.Execute(null);
                    if (SelectedTab == "Summary")
                    {
                        LoadSummaryCommand.Execute(null);
                    }
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error opening add expense dialog: {ex.Message}", "Error");
            }
        }

        private void ShowEditExpenseDialog()
        {
            if (SelectedExpense == null) return;

            try
            {
                var viewModel = _serviceProvider.GetRequiredService<AddEditExpenseViewModel>();
                viewModel.IsEditMode = true;
                viewModel.LoadExpense(SelectedExpense.ExpenseId);
                var dialog = new MVVM.Views.AddEditExpenseDialog(viewModel);
                dialog.Owner = System.Windows.Application.Current.MainWindow;
                if (dialog.ShowDialog() == true)
                {
                    LoadExpensesCommand.Execute(null);
                    if (SelectedTab == "Summary")
                    {
                        LoadSummaryCommand.Execute(null);
                    }
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

            var confirmed = _dialogService.ShowConfirmation(
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
                var filePath = _dialogService.ShowOpenFileDialog("Image Files|*.jpg;*.jpeg;*.png;*.pdf|All Files|*.*", "Select Receipt");
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
