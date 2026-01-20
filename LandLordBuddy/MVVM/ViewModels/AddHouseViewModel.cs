using System;
using System.Windows.Input;
using LandLordBuddy.MVVM.Commands;
using LandLordBuddy.MVVM.Models;
using LandLordBuddy.Services.Business;
using LandLordBuddy.Services.Dialog;

namespace LandLordBuddy.MVVM.ViewModels
{
    public class AddHouseViewModel : BaseViewModel
    {
        private readonly IHouseService _houseService;
        private readonly IDialogService _dialogService;

        private string _addressLine1 = string.Empty;
        private string? _addressLine2;
        private string _city = string.Empty;
        private string _postcode = string.Empty;
        private int _totalRooms = 1;

        public AddHouseViewModel(IHouseService houseService, IDialogService dialogService)
        {
            _houseService = houseService;
            _dialogService = dialogService;
            SaveCommand = new RelayCommand(async _ => await SaveAsync(), _ => CanSave());
        }

        public event EventHandler? CloseRequested;

        public ICommand SaveCommand { get; }

        public string AddressLine1
        {
            get => _addressLine1;
            set
            {
                SetProperty(ref _addressLine1, value);
                ((RelayCommand)SaveCommand).RaiseCanExecuteChanged();
            }
        }

        public string? AddressLine2
        {
            get => _addressLine2;
            set => SetProperty(ref _addressLine2, value);
        }

        public string City
        {
            get => _city;
            set
            {
                SetProperty(ref _city, value);
                ((RelayCommand)SaveCommand).RaiseCanExecuteChanged();
            }
        }

        public string Postcode
        {
            get => _postcode;
            set
            {
                SetProperty(ref _postcode, value);
                ((RelayCommand)SaveCommand).RaiseCanExecuteChanged();
            }
        }

        public int TotalRooms
        {
            get => _totalRooms;
            set => SetProperty(ref _totalRooms, value);
        }

        private bool CanSave()
        {
            return !string.IsNullOrWhiteSpace(AddressLine1) &&
                   !string.IsNullOrWhiteSpace(City) &&
                   !string.IsNullOrWhiteSpace(Postcode) &&
                   TotalRooms > 0;
        }

        private async Task SaveAsync()
        {
            try
            {
                var house = new House
                {
                    AddressLine1 = AddressLine1,
                    AddressLine2 = AddressLine2,
                    City = City,
                    Postcode = Postcode,
                    TotalRooms = TotalRooms
                };

                await _houseService.CreateHouseAsync(house);
                _dialogService.ShowMessage("House added successfully!", "Success");
                CloseRequested?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error adding house: {ex.Message}", "Error");
            }
        }
    }
}
