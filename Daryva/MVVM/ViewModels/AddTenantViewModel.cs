using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Input;
using Daryva.MVVM.Commands;
using Daryva.MVVM.Models;
using Daryva.Services.Business;
using Daryva.Services.Data;
using Daryva.Services.Dialog;

namespace Daryva.MVVM.ViewModels
{
    public class AddTenantViewModel : BaseViewModel
    {
        private readonly ITenantService _tenantService;
        private readonly IHouseService _houseService;
        private readonly ITenancyRepository _tenancyRepository;
        private readonly IDialogService _dialogService;

        private string _fullName = string.Empty;
        private string _email = string.Empty;
        private string _phoneNumber = string.Empty;
        private string? _universityName;
        private House? _selectedHouse;
        private decimal _rentAmountMonthly = 0;
        private decimal _depositAmount = 0;
        private byte _paymentDueDay = 1;

        public AddTenantViewModel(
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
            
            // Load houses asynchronously after construction - use dispatcher to avoid thread issues
            System.Windows.Application.Current?.Dispatcher.BeginInvoke(new Action(async () =>
            {
                try
                {
                    await LoadHousesAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading houses in background: {ex.Message}");
                }
            }));
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
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
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
                    var tenancy = new Tenancy
                    {
                        HouseId = SelectedHouse.HouseId,
                        TenantId = createdTenant.TenantId,
                        MoveInDate = DateTime.Today,
                        MoveOutDate = null,
                        RentAmountMonthly = RentAmountMonthly,
                        DepositAmount = DepositAmount,
                        PaymentDueDay = PaymentDueDay,
                        Status = "Active"
                    };

                    await _tenancyRepository.CreateTenancyAsync(tenancy);
                    _dialogService.ShowMessage($"Tenant added successfully and assigned to {SelectedHouse.AddressLine1}!", "Success");
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
