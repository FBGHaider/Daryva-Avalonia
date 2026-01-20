using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Daryva.MVVM.Commands;
using Daryva.MVVM.Models;
using Daryva.Services.Business;
using Daryva.Services.Data;
using Daryva.Services.Dialog;

namespace Daryva.MVVM.ViewModels
{
    public class RecordPaymentViewModel : BaseViewModel
    {
        private readonly IPaymentService _paymentService;
        private readonly ITenancyRepository _tenancyRepository;
        private readonly IDialogService _dialogService;

        private Tenancy? _selectedTenancy;
        private DateTime _paymentDate = DateTime.Today;
        private string _paymentMethod = "BankTransfer";
        private string? _reference;
        private string? _notes;
        private decimal _depositAmount = 0;
        private decimal _rentAmount = 0;
        private int _rentYear = DateTime.Now.Year;
        private int _rentMonth = DateTime.Now.Month;
        private string _selectedMonthName = DateTime.Now.ToString("MMMM");
        private decimal _totalDepositPaid = 0;
        private decimal _totalRentPaidForPeriod = 0;
        private int? _preselectedTenancyId;

        public RecordPaymentViewModel(
            IPaymentService paymentService,
            ITenancyRepository tenancyRepository,
            IDialogService dialogService)
        {
            _paymentService = paymentService;
            _tenancyRepository = tenancyRepository;
            _dialogService = dialogService;

            Tenancies = new ObservableCollection<Tenancy>();
            SaveCommand = new RelayCommand(async _ => await SaveAsync(), _ => CanSave());

            LoadActiveTenanciesCommand = new RelayCommand(async _ => await LoadActiveTenanciesAsync());
            
            // Don't load data in constructor - it will be loaded after dialog is shown
        }

        /// <summary>
        /// Sets the pre-selected tenancy ID and optionally the month/year for rent payment
        /// </summary>
        public void SetPreselectedTenancy(int tenancyId, int? year = null, int? month = null)
        {
            _preselectedTenancyId = tenancyId;
            if (year.HasValue)
                RentYear = year.Value;
            if (month.HasValue)
                RentMonth = month.Value;
        }

        /// <summary>
        /// Call this method when the dialog is shown to load data
        /// </summary>
        public async Task InitializeAsync()
        {
            try
            {
                // Add timeout to prevent infinite blocking
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(10));
                var loadTask = LoadActiveTenanciesAsync();
                
                var completedTask = await Task.WhenAny(loadTask, timeoutTask);
                
                if (completedTask == timeoutTask)
                {
                    throw new TimeoutException("Loading tenancies timed out. Please check your database connection.");
                }
                
                await loadTask; // Re-await to propagate any exceptions
                
                // After loading tenancies, pre-select if a tenancy ID was provided
                if (_preselectedTenancyId.HasValue)
                {
                    var preselectedTenancy = Tenancies.FirstOrDefault(t => t.TenancyId == _preselectedTenancyId.Value);
                    if (preselectedTenancy != null)
                    {
                        SelectedTenancy = preselectedTenancy;
                    }
                }
            }
            catch (Exception ex)
            {
                
                // Don't show dialog here as it might cause issues - let the dialog handle it
                throw; // Re-throw to be handled by dialog
            }
        }

        public event EventHandler? CloseRequested;

        public ICommand LoadActiveTenanciesCommand { get; }
        public ICommand SaveCommand { get; }

        public ObservableCollection<Tenancy> Tenancies { get; }

        public Tenancy? SelectedTenancy
        {
            get => _selectedTenancy;
            set
            {
                if (SetProperty(ref _selectedTenancy, value))
                {
                    LoadTenancyDetails();
                    ((RelayCommand)SaveCommand).RaiseCanExecuteChanged();
                }
            }
        }

        public DateTime PaymentDate
        {
            get => _paymentDate;
            set => SetProperty(ref _paymentDate, value);
        }

        public string PaymentMethod
        {
            get => _paymentMethod;
            set => SetProperty(ref _paymentMethod, value);
        }

        public string? Reference
        {
            get => _reference;
            set => SetProperty(ref _reference, value);
        }

        public string? Notes
        {
            get => _notes;
            set => SetProperty(ref _notes, value);
        }

        public decimal DepositAmount
        {
            get => _depositAmount;
            set
            {
                if (SetProperty(ref _depositAmount, value))
                {
                    ((RelayCommand)SaveCommand).RaiseCanExecuteChanged();
                }
            }
        }

        public decimal RentAmount
        {
            get => _rentAmount;
            set
            {
                if (SetProperty(ref _rentAmount, value))
                {
                    ((RelayCommand)SaveCommand).RaiseCanExecuteChanged();
                }
            }
        }

        public int RentYear
        {
            get => _rentYear;
            set
            {
                if (SetProperty(ref _rentYear, value))
                {
                    LoadTenancyDetails();
                }
            }
        }

        public int RentMonth
        {
            get => _rentMonth;
            set
            {
                if (SetProperty(ref _rentMonth, value))
                {
                    SelectedMonthName = new DateTime(2000, value, 1).ToString("MMMM");
                    LoadTenancyDetails();
                }
            }
        }

        public string SelectedMonthName
        {
            get => _selectedMonthName;
            set => SetProperty(ref _selectedMonthName, value);
        }

        public decimal TotalDepositPaid
        {
            get => _totalDepositPaid;
            set => SetProperty(ref _totalDepositPaid, value);
        }

        public decimal TotalRentPaidForPeriod
        {
            get => _totalRentPaidForPeriod;
            set => SetProperty(ref _totalRentPaidForPeriod, value);
        }

        public decimal DepositRemaining => SelectedTenancy != null ? Math.Max(0, SelectedTenancy.DepositAmount - TotalDepositPaid) : 0;
        public decimal RentRemaining => SelectedTenancy != null ? Math.Max(0, SelectedTenancy.RentAmountMonthly - TotalRentPaidForPeriod) : 0;
        public decimal TotalPayment => DepositAmount + RentAmount;

        private async Task LoadActiveTenanciesAsync()
        {
            try
            {
                // Run database query on background thread to avoid blocking UI
                var tenancies = await Task.Run(async () =>
                {
                    return await _tenancyRepository.GetActiveTenanciesAsync().ConfigureAwait(false);
                }).ConfigureAwait(false);
                
                // Update UI on UI thread
                var dispatcher = System.Windows.Application.Current.Dispatcher;
                if (dispatcher.CheckAccess())
                {
                    Tenancies.Clear();
                    foreach (var tenancy in tenancies)
                    {
                        Tenancies.Add(tenancy);
                    }
                }
                else
                {
                    await dispatcher.InvokeAsync(() =>
                    {
                        Tenancies.Clear();
                        foreach (var tenancy in tenancies)
                        {
                            Tenancies.Add(tenancy);
                        }
                    });
                }
            }
            catch
            {
                throw; // Re-throw to be handled by caller
            }
        }

        private async void LoadTenancyDetails()
        {
            if (SelectedTenancy == null)
            {
                TotalDepositPaid = 0;
                TotalRentPaidForPeriod = 0;
                DepositAmount = 0;
                RentAmount = 0;
                OnPropertyChanged(nameof(DepositRemaining));
                OnPropertyChanged(nameof(RentRemaining));
                OnPropertyChanged(nameof(TotalPayment));
                return;
            }

            try
            {
                var depositPaid = await _paymentService.GetTotalDepositPaidAsync(SelectedTenancy.TenancyId);
                var rentPaid = await _paymentService.GetTotalRentPaidForPeriodAsync(SelectedTenancy.TenancyId, RentYear, RentMonth);

                // Update on UI thread using BeginInvoke to avoid blocking (fire-and-forget)
                _ = System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        TotalDepositPaid = depositPaid;
                        TotalRentPaidForPeriod = rentPaid;

                        // Auto-suggest remaining amounts
                        DepositAmount = DepositRemaining;
                        RentAmount = RentRemaining;

                        OnPropertyChanged(nameof(DepositRemaining));
                        OnPropertyChanged(nameof(RentRemaining));
                        OnPropertyChanged(nameof(TotalPayment));
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error updating UI in LoadTenancyDetails: {ex.Message}");
                    }
                }));
            }
            catch (Exception ex)
            {
                _dialogService?.ShowMessage($"Error loading tenancy details: {ex.Message}", "Error");
            }
        }

        private bool CanSave()
        {
            return SelectedTenancy != null &&
                   (DepositAmount > 0 || RentAmount > 0) &&
                   DepositAmount <= DepositRemaining + 100 && // Allow small overpayment
                   RentAmount >= 0;
        }

        private async Task SaveAsync()
        {
            if (SelectedTenancy == null) return;

            try
            {
                // Validate deposit amount
                if (DepositAmount > DepositRemaining + 100)
                {
                    _dialogService.ShowMessage($"Deposit payment cannot exceed remaining deposit (£{DepositRemaining:N2}) plus a small overpayment allowance.", "Validation Error");
                    return;
                }

                // Validate rent amount
                if (RentAmount < 0)
                {
                    _dialogService.ShowMessage("Rent amount cannot be negative.", "Validation Error");
                    return;
                }

                        // Record payment
                        await _paymentService.RecordPaymentAsync(
                            SelectedTenancy.TenancyId,
                            DepositAmount,
                            RentAmount,
                            RentYear,
                            RentMonth,
                            PaymentDate,
                            PaymentMethod,
                            Reference,
                            Notes);

                        // Notify dashboard to refresh
                        DashboardViewModel.NotifyPaymentDataChanged();
                        
                        _dialogService.ShowMessage("Payment recorded successfully!", "Success");
                        CloseRequested?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error recording payment: {ex.Message}", "Error");
            }
        }
    }
}
