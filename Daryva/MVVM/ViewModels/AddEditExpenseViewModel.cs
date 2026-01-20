using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Daryva.MVVM.Commands;
using Daryva.MVVM.Models;
using Daryva.Services.Business;
using Daryva.Services.Dialog;

namespace Daryva.MVVM.ViewModels
{
    public class AddEditExpenseViewModel : BaseViewModel
    {
        private readonly IExpenseService _expenseService;
        private readonly IHouseService _houseService;
        private readonly IDialogService _dialogService;

        private bool _isEditMode;
        private int _expenseId;
        private int? _selectedHouseId;
        private DateTime _dateIncurred = DateTime.Today;
        private string _category = "Other";
        private decimal _amount;
        private string _vendor = string.Empty;
        private string _notes = string.Empty;
        private string _paymentMethod = string.Empty;
        private string _reference = string.Empty;
        private bool _hasReceipt;
        private int? _receiptDocumentId;

        public AddEditExpenseViewModel(
            IExpenseService expenseService,
            IHouseService houseService,
            IDialogService dialogService)
        {
            _expenseService = expenseService ?? throw new ArgumentNullException(nameof(expenseService));
            _houseService = houseService ?? throw new ArgumentNullException(nameof(houseService));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));

            Houses = new ObservableCollection<House>();
            Categories = new ObservableCollection<string>
            {
                "Repairs",
                "Bills",
                "CouncilTax",
                "Internet",
                "Furniture",
                "Cleaning",
                "Maintenance",
                "Other"
            };

            SaveCommand = new RelayCommand(async _ => await SaveAsync(), _ => CanSave());
            SaveAndAddAnotherCommand = new RelayCommand(async _ => await SaveAndAddAnotherAsync(), _ => CanSave());
            CancelCommand = new RelayCommand(_ => Cancel());
            AttachReceiptCommand = new RelayCommand(async _ => await AttachReceiptAsync());

            _ = LoadHousesAsync();
        }

        public ICommand SaveCommand { get; }
        public ICommand SaveAndAddAnotherCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand AttachReceiptCommand { get; }

        public ObservableCollection<House> Houses { get; }
        public ObservableCollection<string> Categories { get; }

        public bool IsEditMode
        {
            get => _isEditMode;
            set => SetProperty(ref _isEditMode, value);
        }

        public int? SelectedHouseId
        {
            get => _selectedHouseId;
            set => SetProperty(ref _selectedHouseId, value);
        }

        public DateTime DateIncurred
        {
            get => _dateIncurred;
            set => SetProperty(ref _dateIncurred, value);
        }

        public string Category
        {
            get => _category;
            set => SetProperty(ref _category, value);
        }

        public decimal Amount
        {
            get => _amount;
            set
            {
                SetProperty(ref _amount, value);
                ((RelayCommand)SaveCommand).RaiseCanExecuteChanged();
                ((RelayCommand)SaveAndAddAnotherCommand).RaiseCanExecuteChanged();
            }
        }

        public string Vendor
        {
            get => _vendor;
            set => SetProperty(ref _vendor, value);
        }

        public string Notes
        {
            get => _notes;
            set => SetProperty(ref _notes, value);
        }

        public string PaymentMethod
        {
            get => _paymentMethod;
            set => SetProperty(ref _paymentMethod, value);
        }

        public string Reference
        {
            get => _reference;
            set => SetProperty(ref _reference, value);
        }

        public bool HasReceipt
        {
            get => _hasReceipt;
            set => SetProperty(ref _hasReceipt, value);
        }

        public event EventHandler<bool>? CloseRequested;

        private async Task LoadHousesAsync()
        {
            try
            {
                var houses = await _houseService.GetAllHousesAsync();
                Houses.Clear();
                foreach (var house in houses)
                {
                    Houses.Add(house);
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error loading houses: {ex.Message}", "Error");
            }
        }

        public async Task LoadExpense(int expenseId)
        {
            try
            {
                var expense = await _expenseService.GetExpenseByIdAsync(expenseId);
                if (expense == null)
                {
                    _dialogService.ShowMessage("Expense not found.", "Error");
                    return;
                }

                _expenseId = expense.HouseExpenseId;
                SelectedHouseId = expense.HouseId;
                DateIncurred = expense.DateIncurred;
                Category = expense.Category;
                Amount = expense.Amount;
                Vendor = expense.Vendor ?? string.Empty;
                Notes = expense.Notes ?? string.Empty;
                HasReceipt = expense.ReceiptDocumentId.HasValue;
                _receiptDocumentId = expense.ReceiptDocumentId;
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error loading expense: {ex.Message}", "Error");
            }
        }

        private bool CanSave()
        {
            return SelectedHouseId.HasValue && Amount > 0;
        }

        private async Task SaveAsync()
        {
            if (!CanSave())
            {
                _dialogService.ShowMessage("Please fill in all required fields (House and Amount).", "Validation Error");
                return;
            }

            try
            {
                var expense = new HouseExpense
                {
                    HouseId = SelectedHouseId!.Value,
                    DateIncurred = DateIncurred,
                    Category = Category,
                    Amount = Amount,
                    Vendor = string.IsNullOrWhiteSpace(Vendor) ? null : Vendor,
                    Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes,
                    ReceiptDocumentId = _receiptDocumentId
                };

                if (IsEditMode)
                {
                    expense.HouseExpenseId = _expenseId;
                    await _expenseService.UpdateExpenseAsync(expense);
                    _dialogService.ShowMessage("Expense updated successfully.", "Success");
                }
                else
                {
                    await _expenseService.CreateExpenseAsync(expense);
                    _dialogService.ShowMessage("Expense created successfully.", "Success");
                }

                CloseRequested?.Invoke(this, true);
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error saving expense: {ex.Message}", "Error");
            }
        }

        private async Task SaveAndAddAnotherAsync()
        {
            if (!CanSave())
            {
                _dialogService.ShowMessage("Please fill in all required fields (House and Amount).", "Validation Error");
                return;
            }

            try
            {
                var expense = new HouseExpense
                {
                    HouseId = SelectedHouseId!.Value,
                    DateIncurred = DateIncurred,
                    Category = Category,
                    Amount = Amount,
                    Vendor = string.IsNullOrWhiteSpace(Vendor) ? null : Vendor,
                    Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes,
                    ReceiptDocumentId = _receiptDocumentId
                };

                await _expenseService.CreateExpenseAsync(expense);
                _dialogService.ShowMessage("Expense created successfully.", "Success");

                // Reset form for next entry
                SelectedHouseId = null;
                DateIncurred = DateTime.Today;
                Category = "Other";
                Amount = 0;
                Vendor = string.Empty;
                Notes = string.Empty;
                PaymentMethod = string.Empty;
                Reference = string.Empty;
                HasReceipt = false;
                _receiptDocumentId = null;
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error saving expense: {ex.Message}", "Error");
            }
        }

        private Task AttachReceiptAsync()
        {
            if (!SelectedHouseId.HasValue)
            {
                _dialogService.ShowMessage("Please select a house first.", "Validation Error");
                return Task.CompletedTask;
            }

            try
            {
                var filePath = _dialogService.ShowOpenFileDialog("Image Files|*.jpg;*.jpeg;*.png;*.pdf|All Files|*.*", "Select Receipt");
                if (string.IsNullOrWhiteSpace(filePath))
                    return Task.CompletedTask;

                // Note: Receipt attachment will be handled after expense is saved
                _dialogService.ShowMessage("Receipt attachment will be available after saving the expense. You can attach it from the expense list.", "Info");
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error selecting receipt: {ex.Message}", "Error");
            }
            
            return Task.CompletedTask;
        }

        private void Cancel()
        {
            CloseRequested?.Invoke(this, false);
        }
    }
}
