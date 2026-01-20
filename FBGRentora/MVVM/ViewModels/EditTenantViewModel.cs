using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Input;
using FBGRentora.MVVM.Commands;
using FBGRentora.MVVM.Models;
using FBGRentora.Services.Business;
using FBGRentora.Services.Data;
using FBGRentora.Services.Dialog;

namespace FBGRentora.MVVM.ViewModels
{
    public class EditTenantViewModel : BaseViewModel
    {
        private readonly ITenantService _tenantService;
        private readonly IHouseService _houseService;
        private readonly ITenancyRepository _tenancyRepository;
        private readonly IDialogService _dialogService;

        private int _tenantId;
        private int? _currentTenancyId;
        private string _fullName = string.Empty;
        private string _email = string.Empty;
        private string _phoneNumber = string.Empty;
        private string? _universityName;
        private House? _selectedHouse;
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
            FullName = tenant.FullName;
            Email = tenant.Email;
            PhoneNumber = tenant.PhoneNumber;
            UniversityName = tenant.UniversityName;

            // Load current tenancy if exists
            var tenancies = await _tenancyRepository.GetTenanciesByTenantIdAsync(tenant.TenantId);
            var activeTenancy = tenancies.FirstOrDefault(t => t.Status == "Active" && t.MoveOutDate == null);
            
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

                // Check for duplicate email (excluding current tenant)
                var existingTenants = await _tenantService.GetAllTenantsAsync();
                if (existingTenants.Any(t => t.TenantId != TenantId && t.Email.Equals(trimmedEmail, StringComparison.OrdinalIgnoreCase)))
                {
                    _dialogService.ShowMessage($"A tenant with email '{trimmedEmail}' already exists. Please use a different email address.", "Duplicate Email");
                    return;
                }

                // Update tenant
                var tenant = new Tenant
                {
                    TenantId = TenantId,
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
                            existingTenancy.HouseId = SelectedHouse.HouseId;
                            existingTenancy.RentAmountMonthly = RentAmountMonthly;
                            existingTenancy.DepositAmount = DepositAmount;
                            existingTenancy.PaymentDueDay = PaymentDueDay;
                            await _tenancyRepository.UpdateTenancyAsync(existingTenancy);
                        }
                    }
                    else
                    {
                        // Create new tenancy
                        var tenancy = new Tenancy
                        {
                            HouseId = SelectedHouse.HouseId,
                            TenantId = TenantId,
                            MoveInDate = DateTime.Today,
                            MoveOutDate = null,
                            RentAmountMonthly = RentAmountMonthly,
                            DepositAmount = DepositAmount,
                            PaymentDueDay = PaymentDueDay,
                            Status = "Active"
                        };
                        await _tenancyRepository.CreateTenancyAsync(tenancy);
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
    }
}
