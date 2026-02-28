using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
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
    public class EditTenantViewModel : BaseViewModel
    {
        private readonly ITenantService _tenantService;
        private readonly IHouseService _houseService;
        private readonly ITenancyRepository _tenancyRepository;
        private readonly IDialogService _dialogService;

        private int _tenantId;
        private Guid? _tenantApiId; // Required for API backend when updating tenant
        private int? _currentTenancyId;
        private string _fullName = string.Empty;
        private string _email = string.Empty;
        private string _phoneNumber = string.Empty;
        private string? _universityName;
        private House? _selectedHouse;
        private DateTimeOffset _moveInDate = new DateTimeOffset(DateTime.Today);
        private string _rentStartOption = "Same month as move-in";
        private decimal _rentAmountMonthly = 0;
        private decimal _depositAmount = 0;
        private byte _paymentDueDay = 1;

        public EditTenantViewModel(
            ITenantService tenantService,
            IHouseService houseService,
            ITenancyRepository tenancyRepository,
            IDialogService dialogService)
        {
            _tenantService = tenantService;
            _houseService = houseService;
            _tenancyRepository = tenancyRepository;
            _dialogService = dialogService;
            
            Houses = new ObservableCollection<House>();
            SaveCommand = new RelayCommand(async _ => await SaveAsync(), _ => CanSave());
            LoadHousesCommand = new RelayCommand(async _ => await LoadHousesAsync());
            
            // Load houses asynchronously after construction
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                _ = LoadHousesAsync().ContinueWith(task =>
                {
                    if (task.IsFaulted)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error loading houses in background: {task.Exception?.GetBaseException()?.Message}");
                    }
                });
            });
        }

        public event EventHandler? CloseRequested;

        public ICommand SaveCommand { get; }
        public ICommand LoadHousesCommand { get; }

        public ObservableCollection<House> Houses { get; }

        public int TenantId
        {
            get => _tenantId;
            private set => SetProperty(ref _tenantId, value);
        }

        public string FullName
        {
            get => _fullName;
            set
            {
                SetProperty(ref _fullName, value);
                ((RelayCommand)SaveCommand).RaiseCanExecuteChanged();
            }
        }

        public string Email
        {
            get => _email;
            set
            {
                SetProperty(ref _email, value);
                ((RelayCommand)SaveCommand).RaiseCanExecuteChanged();
            }
        }

        public string PhoneNumber
        {
            get => _phoneNumber;
            set
            {
                SetProperty(ref _phoneNumber, value);
                ((RelayCommand)SaveCommand).RaiseCanExecuteChanged();
            }
        }

        public string? UniversityName
        {
            get => _universityName;
            set => SetProperty(ref _universityName, value);
        }

        public House? SelectedHouse
        {
            get => _selectedHouse;
            set
            {
                SetProperty(ref _selectedHouse, value);
                ((RelayCommand)SaveCommand).RaiseCanExecuteChanged();
            }
        }

        public DateTimeOffset MoveInDate
        {
            get => _moveInDate;
            set => SetProperty(ref _moveInDate, value);
        }

        public string RentStartOption
        {
            get => _rentStartOption;
            set => SetProperty(ref _rentStartOption, value);
        }

        public ObservableCollection<string> RentStartOptions { get; } = new ObservableCollection<string>
        {
            "Same month as move-in",
            "Next month after move-in"
        };

        public decimal RentAmountMonthly
        {
            get => _rentAmountMonthly;
            set => SetProperty(ref _rentAmountMonthly, value);
        }

        public decimal DepositAmount
        {
            get => _depositAmount;
            set => SetProperty(ref _depositAmount, value);
        }

        public byte PaymentDueDay
        {
            get => _paymentDueDay;
            set => SetProperty(ref _paymentDueDay, value);
        }

        public async Task LoadTenantAsync(Tenant tenant)
        {
            if (tenant == null) return;

            TenantId = tenant.TenantId;
            _tenantApiId = tenant.ApiId; // Required for API backend when updating
            FullName = tenant.FullName;
            Email = tenant.Email;
            PhoneNumber = tenant.PhoneNumber;
            UniversityName = tenant.UniversityName;

            // Load current tenancy if exists
            var tenancies = (await _tenancyRepository.GetTenanciesByTenantIdAsync(tenant.TenantId)).ToList();
            var activeTenancy = tenancies.FirstOrDefault(t =>
                string.Equals(t.Status, "Active", StringComparison.OrdinalIgnoreCase) && t.MoveOutDate == null);
            
            if (activeTenancy != null)
            {
                _currentTenancyId = activeTenancy.TenancyId;
                
                // Load houses first if not already loaded
                if (Houses.Count == 0)
                {
                    await LoadHousesAsync();
                }
                
                // Find and select the house
                SelectedHouse = Houses.FirstOrDefault(h => h.HouseId == activeTenancy.HouseId);
                MoveInDate = new DateTimeOffset(activeTenancy.MoveInDate);
                RentStartOption = GetRentStartOptionFromTenancy(activeTenancy);
                RentAmountMonthly = activeTenancy.RentAmountMonthly;
                DepositAmount = activeTenancy.DepositAmount;
                PaymentDueDay = activeTenancy.PaymentDueDay;
            }
        }

        private async Task LoadHousesAsync()
        {
            try
            {
                var houses = await _houseService.GetAllHousesAsync();
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    Houses.Clear();
                    foreach (var house in houses)
                    {
                        Houses.Add(house);
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading houses: {ex.Message}");
                _dialogService?.ShowMessage($"Error loading houses: {ex.Message}", "Error");
            }
        }

        private bool CanSave()
        {
            return !string.IsNullOrWhiteSpace(FullName) &&
                   !string.IsNullOrWhiteSpace(Email) &&
                   !string.IsNullOrWhiteSpace(PhoneNumber) &&
                   SelectedHouse != null;
        }

        private async Task SaveAsync()
        {
            try
            {
                // Validate and trim email
                var trimmedEmail = Email?.Trim();
                if (string.IsNullOrWhiteSpace(trimmedEmail))
                {
                    _dialogService.ShowMessage("Email is required and cannot be empty.", "Validation Error");
                    return;
                }

                // Basic email format validation
                var emailPattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
                if (!Regex.IsMatch(trimmedEmail, emailPattern, RegexOptions.IgnoreCase))
                {
                    _dialogService.ShowMessage("Please enter a valid email address.", "Validation Error");
                    return;
                }

                // Check for duplicate email (excluding current tenant)
                var existingTenants = await _tenantService.GetAllTenantsAsync();
                if (existingTenants.Any(t => t.TenantId != TenantId && t.Email.Equals(trimmedEmail, StringComparison.OrdinalIgnoreCase)))
                {
                    _dialogService.ShowMessage($"A tenant with email '{trimmedEmail}' already exists. Please use a different email address.", "Duplicate Email");
                    return;
                }

                // Resolve ApiId if missing (e.g. tenant list from cache or different source)
                var apiId = _tenantApiId;
                if (!apiId.HasValue)
                {
                    var existing = (await _tenantService.GetAllTenantsAsync(includeArchived: true))
                        .FirstOrDefault(t => t.TenantId == TenantId);
                    apiId = existing?.ApiId;
                }

                // Update tenant (ApiId required when using API backend)
                var tenant = new Tenant
                {
                    TenantId = TenantId,
                    ApiId = apiId,
                    FullName = FullName.Trim(),
                    Email = trimmedEmail,
                    PhoneNumber = PhoneNumber.Trim(),
                    UniversityName = UniversityName?.Trim()
                };

                await _tenantService.UpdateTenantAsync(tenant);

                // Update or create tenancy if house is selected
                if (SelectedHouse != null)
                {
                    if (_currentTenancyId.HasValue)
                    {
                        // Update existing tenancy
                        var existingTenancy = await _tenancyRepository.GetTenancyByIdAsync(_currentTenancyId.Value);
                        if (existingTenancy != null)
                        {
                            var moveIn = MoveInDate.DateTime.Date;
                            var isNextMonth = string.Equals(RentStartOption?.Trim(), "Next month after move-in", StringComparison.OrdinalIgnoreCase);
                            var (rentStartMonth, rentStartYear) = isNextMonth
                                ? (moveIn.AddMonths(1).Month, moveIn.AddMonths(1).Year)
                                : (moveIn.Month, moveIn.Year);

                            existingTenancy.HouseId = SelectedHouse.HouseId;
                            existingTenancy.MoveInDate = moveIn;
                            existingTenancy.RentStartMonth = rentStartMonth;
                            existingTenancy.RentStartYear = rentStartYear;
                            existingTenancy.RentAmountMonthly = RentAmountMonthly;
                            existingTenancy.DepositAmount = DepositAmount;
                            existingTenancy.PaymentDueDay = PaymentDueDay;
                            await _tenancyRepository.UpdateTenancyAsync(existingTenancy);
                        }
                    }
                    else
                    {
                        // No _currentTenancyId - check for existing active tenancy at this house, or ended tenancy (e.g. recovered tenant) to reactivate
                        var tenantTenancies = (await _tenancyRepository.GetTenanciesByTenantIdAsync(TenantId)).ToList();
                        var existingAtHouse = tenantTenancies.FirstOrDefault(t =>
                            t.HouseId == SelectedHouse.HouseId &&
                            string.Equals(t.Status, "Active", StringComparison.OrdinalIgnoreCase) &&
                            t.MoveOutDate == null);
                        var endedAtHouse = existingAtHouse == null
                            ? tenantTenancies
                                .Where(t => t.HouseId == SelectedHouse.HouseId &&
                                    string.Equals(t.Status, "Ended", StringComparison.OrdinalIgnoreCase) && t.MoveOutDate.HasValue)
                                .OrderByDescending(t => t.MoveOutDate)
                                .FirstOrDefault()
                            : null;

                        var moveIn = MoveInDate.DateTime.Date;
                        var isNextMonth = string.Equals(RentStartOption?.Trim(), "Next month after move-in", StringComparison.OrdinalIgnoreCase);
                        var (rentStartMonth, rentStartYear) = isNextMonth
                            ? (moveIn.AddMonths(1).Month, moveIn.AddMonths(1).Year)
                            : (moveIn.Month, moveIn.Year);

                        if (existingAtHouse != null)
                        {
                            // Update existing tenancy instead of creating duplicate
                            existingAtHouse.MoveInDate = moveIn;
                            existingAtHouse.RentStartMonth = rentStartMonth;
                            existingAtHouse.RentStartYear = rentStartYear;
                            existingAtHouse.RentAmountMonthly = RentAmountMonthly;
                            existingAtHouse.DepositAmount = DepositAmount;
                            existingAtHouse.PaymentDueDay = PaymentDueDay;
                            await _tenancyRepository.UpdateTenancyAsync(existingAtHouse);
                        }
                        else if (endedAtHouse != null)
                        {
                            // Reactivate ended tenancy at this house (recovered tenant) instead of creating duplicate
                            endedAtHouse.MoveInDate = moveIn;
                            endedAtHouse.MoveOutDate = null;
                            endedAtHouse.RentStartMonth = rentStartMonth;
                            endedAtHouse.RentStartYear = rentStartYear;
                            endedAtHouse.RentAmountMonthly = RentAmountMonthly;
                            endedAtHouse.DepositAmount = DepositAmount;
                            endedAtHouse.PaymentDueDay = PaymentDueDay;
                            endedAtHouse.Status = "Active";
                            await _tenancyRepository.UpdateTenancyAsync(endedAtHouse);
                        }
                        else
                        {
                            // Truly new tenancy (new assignment to a house)
                            var tenancy = new Tenancy
                            {
                                HouseId = SelectedHouse.HouseId,
                                TenantId = TenantId,
                                MoveInDate = moveIn,
                                MoveOutDate = null,
                                RentStartMonth = rentStartMonth,
                                RentStartYear = rentStartYear,
                                RentAmountMonthly = RentAmountMonthly,
                                DepositAmount = DepositAmount,
                                PaymentDueDay = PaymentDueDay,
                                Status = "Active"
                            };
                            await _tenancyRepository.CreateTenancyAsync(tenancy);
                        }
                    }
                    _dialogService.ShowMessage($"Tenant updated successfully and assigned to {SelectedHouse.AddressLine1}!", "Success");
                }
                else
                {
                    _dialogService.ShowMessage("Tenant updated successfully!", "Success");
                }

                CloseRequested?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error updating tenant: {ex.Message}", "Error");
            }
        }

        private static string GetRentStartOptionFromTenancy(Tenancy tenancy)
        {
            var rentStartYear = tenancy.RentStartYear ?? tenancy.MoveInDate.Year;
            var rentStartMonth = tenancy.RentStartMonth ?? tenancy.MoveInDate.Month;
            var nextMonth = tenancy.MoveInDate.AddMonths(1);
            return (rentStartYear == nextMonth.Year && rentStartMonth == nextMonth.Month)
                ? "Next month after move-in"
                : "Same month as move-in";
        }
    }
}
