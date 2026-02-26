using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Threading;
using Daryva.MVVM.Commands;
using Daryva.MVVM.Models;
using Daryva.Services.Api;
using Daryva.Services.Business;
using Daryva.Services.Dialog;

namespace Daryva.MVVM.ViewModels
{
    public class AddTenantViewModel : BaseViewModel
    {
        private readonly ITenantService _tenantService;
        private readonly IHouseService _houseService;
        private readonly ITenancyApiService? _tenancyApiService;
        private readonly IDialogService _dialogService;
        private readonly ISettingsService _settingsService;

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

        public AddTenantViewModel(
            ITenantService tenantService, 
            IHouseService houseService,
            IDialogService dialogService,
            ISettingsService settingsService,
            ITenancyApiService? tenancyApiService = null)
        {
            _tenantService = tenantService;
            _houseService = houseService;
            _tenancyApiService = tenancyApiService;
            _dialogService = dialogService;
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            
            Houses = new ObservableCollection<House>();
            SaveCommand = new RelayCommand(async _ => await SaveAsync(), _ => CanSave());
            
            LoadHousesCommand = new RelayCommand(async _ => await LoadHousesAsync());
            
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                _ = LoadHousesAsync().ContinueWith(_ => { });
                _ = LoadDefaultRentDueDayAsync();
            });
        }

        public event EventHandler? CloseRequested;

        public ICommand SaveCommand { get; }
        public ICommand LoadHousesCommand { get; }

        public ObservableCollection<House> Houses { get; }

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

        private async Task LoadHousesAsync()
        {
            try
            {
                var houses = await _houseService.GetAllHousesAsync();
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
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

        private async Task LoadDefaultRentDueDayAsync()
        {
            try
            {
                var day = await _settingsService.GetSettingAsync<int>("DefaultRentDueDay", 1) ?? 1;
                if (day >= 1 && day <= 28)
                {
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        _paymentDueDay = (byte)day;
                        OnPropertyChanged(nameof(PaymentDueDay));
                    });
                }
            }
            catch { /* ignore */ }
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

                // Check for duplicate email
                var existingTenants = await _tenantService.GetAllTenantsAsync();
                if (existingTenants.Any(t => t.Email.Equals(trimmedEmail, StringComparison.OrdinalIgnoreCase)))
                {
                    _dialogService.ShowMessage($"A tenant with email '{trimmedEmail}' already exists. Please use a different email address.", "Duplicate Email");
                    return;
                }

                // Create tenant
                var tenant = new Tenant
                {
                    FullName = FullName.Trim(),
                    Email = trimmedEmail,
                    PhoneNumber = PhoneNumber.Trim(),
                    UniversityName = UniversityName?.Trim()
                };

                var createdTenant = await _tenantService.CreateTenantAsync(tenant);

                // Create tenancy if house is selected
                if (SelectedHouse != null)
                {
                    if (RentAmountMonthly <= 0)
                    {
                        _dialogService.ShowMessage("Please enter a rent amount greater than 0 when assigning to a house.", "Validation Error");
                        return;
                    }
                    var moveIn = MoveInDate.DateTime;
                    var isNextMonth = string.Equals(RentStartOption?.Trim(), "Next month after move-in", StringComparison.OrdinalIgnoreCase);
                    var (rentStartMonth, rentStartYear) = isNextMonth
                        ? (moveIn.AddMonths(1).Month, moveIn.AddMonths(1).Year)
                        : (moveIn.Month, moveIn.Year);

                    // API-only: create tenancy via API only (no SQLite)
                    if (_tenancyApiService != null && createdTenant.ApiId.HasValue && SelectedHouse.ApiId.HasValue)
                    {
                        var dto = new CreateTenancyDto
                        {
                            HouseId = SelectedHouse.ApiId.Value,
                            TenantId = createdTenant.ApiId.Value,
                            MoveInDate = moveIn.Date,
                            MoveOutDate = null,
                            RentStartMonth = rentStartMonth,
                            RentStartYear = rentStartYear,
                            RentAmountMonthly = RentAmountMonthly,
                            DepositAmount = DepositAmount,
                            PaymentDueDay = PaymentDueDay,
                            Status = "Active"
                        };
                        await _tenancyApiService.CreateTenancyAsync(dto);
                        _dialogService.ShowMessage($"Tenant added successfully and assigned to {SelectedHouse.AddressLine1}!", "Success");
                    }
                    else
                    {
                        _dialogService.ShowMessage("Tenant added successfully. You can assign them to a property from the tenant list.", "Tenant added");
                    }
                }
                else
                {
                    _dialogService.ShowMessage("Tenant added successfully!", "Success");
                }

                CloseRequested?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error adding tenant: {ex.Message}", "Error");
            }
        }
    }
}
