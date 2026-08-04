using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Threading;
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
        private DateTimeOffset? _paymentDate = DateTimeOffset.Now;
        private string _paymentMethod = "Bank Transfer";
        private string? _reference;
        private string? _notes;
        private string _collectedBy = "Abbas";
        private decimal _depositAmount = 0;
        private decimal _rentAmount = 0;
        private int _rentYear = DateTime.Now.Year;
        private int _rentMonth = DateTime.Now.Month;
        private string _selectedMonthName = DateTime.Now.ToString("MMMM");
        private decimal _totalDepositPaid = 0;
        private decimal _totalRentPaidForPeriod = 0;
        private int? _preselectedTenancyId;
        private bool _useDepositForRent;
        private bool _isSaving;

        public RecordPaymentViewModel(
            IPaymentService paymentService,
            ITenancyRepository tenancyRepository,
            IDialogService dialogService)
        {
            _paymentService = paymentService;
            _tenancyRepository = tenancyRepository;
            _dialogService = dialogService;

            Tenancies = new ObservableCollection<Tenancy>();
            PaymentMethodOptions = new ObservableCollection<string> { "Bank Transfer", "Cash", "Card", "Other" };
            MonthOptions = new ObservableCollection<string> { "January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December" };
            YearOptions = new ObservableCollection<int>();
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
                // Populate year options (current year +/- 5)
                YearOptions.Clear();
                var currentYear = DateTime.Now.Year;
                for (int y = currentYear - 5; y <= currentYear + 5; y++)
                    YearOptions.Add(y);
                if (!YearOptions.Contains(RentYear))
                    YearOptions.Add(RentYear);

                if (_preselectedTenancyId.HasValue)
                {
                    var preselectedTenancy = Tenancies.FirstOrDefault(t => t.TenancyId == _preselectedTenancyId.Value);
                    if (preselectedTenancy == null)
                    {
                        // Tenancy not in active list (e.g. ended) - fetch by ID so deposit ledger row can still be used
                        preselectedTenancy = await Task.Run(async () =>
                            await _tenancyRepository.GetTenancyByIdAsync(_preselectedTenancyId.Value).ConfigureAwait(false)).ConfigureAwait(false);
                        if (preselectedTenancy != null)
                        {
                            var dispatcher = Avalonia.Threading.Dispatcher.UIThread;
                            if (dispatcher.CheckAccess())
                            {
                                Tenancies.Add(preselectedTenancy);
                                SelectedTenancy = preselectedTenancy;
                            }
                            else
                            {
                                await dispatcher.InvokeAsync(() =>
                                {
                                    Tenancies.Add(preselectedTenancy);
                                    SelectedTenancy = preselectedTenancy;
                                });
                            }
                        }
                    }
                    if (preselectedTenancy != null)
                    {
                        SelectedTenancy = preselectedTenancy;
                    }
                }
            }
            catch (Exception)
            {
                
                // Don't show dialog here as it might cause issues - let the dialog handle it
                throw; // Re-throw to be handled by dialog
            }
        }

        public event EventHandler? CloseRequested;

        public ICommand LoadActiveTenanciesCommand { get; }
        public ICommand SaveCommand { get; }

        public ObservableCollection<Tenancy> Tenancies { get; }
        public ObservableCollection<string> PaymentMethodOptions { get; }
        public ObservableCollection<string> MonthOptions { get; }
        public ObservableCollection<int> YearOptions { get; }

        public Tenancy? SelectedTenancy
        {
            get => _selectedTenancy;
            set
            {
                if (SetProperty(ref _selectedTenancy, value))
                {
                    RaiseSelectedTenancyDisplayChanged();
                    LoadTenancyDetails();
                    ((RelayCommand)SaveCommand).RaiseCanExecuteChanged();
                }
            }
        }

        /// <summary>Null-safe display values for bindings when SelectedTenancy is null (avoids binding errors).</summary>
        public string SelectedTenancyAddress => _selectedTenancy?.House?.AddressLine1 ?? "";
        public string SelectedTenancyTenantName => _selectedTenancy?.Tenant?.FullName ?? "";
        public decimal SelectedTenancyRentAmount => _selectedTenancy?.RentAmountMonthly ?? 0;
        public decimal SelectedTenancyDepositAmount => _selectedTenancy?.DepositAmount ?? 0;

        private void RaiseSelectedTenancyDisplayChanged()
        {
            OnPropertyChanged(nameof(SelectedTenancyAddress));
            OnPropertyChanged(nameof(SelectedTenancyTenantName));
            OnPropertyChanged(nameof(SelectedTenancyRentAmount));
            OnPropertyChanged(nameof(SelectedTenancyDepositAmount));
            OnPropertyChanged(nameof(DepositRemaining));
            OnPropertyChanged(nameof(RentRemaining));
        }

        public DateTimeOffset? PaymentDate
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

        public string CollectedBy
        {
            get => _collectedBy;
            set => SetProperty(ref _collectedBy, value);
        }

        public decimal DepositAmount
        {
            get => _depositAmount;
            set
            {
                if (SetProperty(ref _depositAmount, value))
                {
                    OnPropertyChanged(nameof(TotalPayment));
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
                    OnPropertyChanged(nameof(TotalPayment));
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
        /// <summary>When using deposit for rent, shows the rent amount (from deposit); otherwise deposit + rent.</summary>
        public decimal TotalPayment => UseDepositForRent ? RentAmount : (DepositAmount + RentAmount);

        /// <summary>When true, use existing deposit paid as the source for this month's rent (no new money; reduces deposit to return).</summary>
        public bool UseDepositForRent
        {
            get => _useDepositForRent;
            set
            {
                if (SetProperty(ref _useDepositForRent, value))
                {
                    ApplyUseDepositForRentIfChecked();
                    OnPropertyChanged(nameof(TotalPayment)); // After applying amounts so total reflects rent from deposit
                    ((RelayCommand)SaveCommand).RaiseCanExecuteChanged();
                }
            }
        }

        /// <summary>True when tenant has deposit paid and rent remaining for the selected period, so "use deposit for rent" is available.</summary>
        public bool CanUseDepositForRent => SelectedTenancy != null && TotalDepositPaid > 0 && RentRemaining > 0;

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
                var dispatcher = Avalonia.Threading.Dispatcher.UIThread;
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

        private void ApplyUseDepositForRentIfChecked()
        {
            if (!UseDepositForRent) return;
            var amountFromDeposit = Math.Min(TotalDepositPaid, RentRemaining);
            DepositAmount = 0;
            RentAmount = amountFromDeposit;
            OnPropertyChanged(nameof(DepositRemaining));
            OnPropertyChanged(nameof(RentRemaining));
            OnPropertyChanged(nameof(TotalPayment));
            OnPropertyChanged(nameof(CanUseDepositForRent));
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
                OnPropertyChanged(nameof(CanUseDepositForRent));
                return;
            }

            try
            {
                var depositPaid = await _paymentService.GetTotalDepositPaidAsync(SelectedTenancy.TenancyId);
                var rentPaid = await _paymentService.GetTotalRentPaidForPeriodAsync(SelectedTenancy.TenancyId, RentYear, RentMonth);

                // Update on UI thread using Post to avoid blocking (fire-and-forget)
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    try
                    {
                        TotalDepositPaid = depositPaid;
                        TotalRentPaidForPeriod = rentPaid;

                        if (UseDepositForRent)
                            ApplyUseDepositForRentIfChecked();
                        else
                        {
                            DepositAmount = DepositRemaining;
                            RentAmount = RentRemaining;
                        }

                        OnPropertyChanged(nameof(DepositRemaining));
                        OnPropertyChanged(nameof(RentRemaining));
                        OnPropertyChanged(nameof(TotalPayment));
                        OnPropertyChanged(nameof(CanUseDepositForRent));
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error updating UI in LoadTenancyDetails: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                _dialogService?.ShowMessage($"Error loading tenancy details: {ex.Message}", "Error");
            }
        }

        private bool CanSave()
        {
            if (_isSaving) return false;
            if (SelectedTenancy == null || !PaymentDate.HasValue) return false;
            if (UseDepositForRent)
                return RentAmount > 0 && DepositAmount == 0 && RentAmount <= TotalDepositPaid && RentAmount <= RentRemaining + 100;
            return (DepositAmount > 0 || RentAmount > 0) &&
                   DepositAmount >= 0 &&
                   DepositAmount <= DepositRemaining + 100 &&
                   RentAmount >= 0;
        }

        private async Task SaveAsync()
        {
            // Guard set before any validation/await, and CanSave() re-checked here too: RelayCommand
            // doesn't re-consult CanExecute between the click and Execute firing, so without this a
            // fast double-click on Save before the first API call returns would send two separate
            // RecordPaymentAsync requests, creating two payment rows for one user action.
            if (_isSaving || !CanSave() || SelectedTenancy == null)
                return;

            _isSaving = true;
            ((RelayCommand)SaveCommand).RaiseCanExecuteChanged();
            try
            {
                // Validate deposit amount -- explicit negative check first: DepositAmount <= DepositRemaining+100
                // alone doesn't catch a negative value (it's trivially "not more than" any positive remaining
                // amount), and the server silently no-ops a non-positive DepositAmount rather than erroring,
                // which previously meant a typo'd negative deposit showed "Payment recorded successfully!"
                // while quietly recording nothing.
                if (DepositAmount < 0)
                {
                    _dialogService.ShowMessage("Deposit amount cannot be negative.", "Validation Error");
                    return;
                }

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

                if (DepositAmount == 0 && RentAmount == 0)
                {
                    _dialogService.ShowMessage("Enter a deposit and/or rent amount greater than zero.", "Validation Error");
                    return;
                }

                if (UseDepositForRent)
                {
                    if (RentAmount > TotalDepositPaid)
                    {
                        _dialogService.ShowMessage($"Rent amount cannot exceed deposit paid (£{TotalDepositPaid:N2}) when using deposit for rent.", "Validation Error");
                        return;
                    }
                }

                // Record payment (useDepositForRent: record rent only, marked as paid from deposit; no new deposit payment)
                await _paymentService.RecordPaymentAsync(
                    SelectedTenancy.TenancyId,
                    UseDepositForRent ? 0 : DepositAmount,
                    RentAmount,
                    RentYear,
                    RentMonth,
                    PaymentDate!.Value.DateTime,
                    PaymentMethod,
                    Reference,
                    Notes,
                    CollectedBy,
                    UseDepositForRent);

                // Notify dashboard to refresh
                DashboardViewModel.NotifyPaymentDataChanged();

                _dialogService.ShowMessage("Payment recorded successfully!", "Success");
                CloseRequested?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error recording payment: {ex.Message}", "Error");
            }
            finally
            {
                _isSaving = false;
                ((RelayCommand)SaveCommand).RaiseCanExecuteChanged();
            }
        }
    }
}
