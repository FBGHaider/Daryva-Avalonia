using System;
using System.Windows.Input;
using Daryva.MVVM.Commands;
using Daryva.MVVM.Models;
using Daryva.Services.Business;
using Daryva.Services.Dialog;

namespace Daryva.MVVM.ViewModels
{
    public class AddHouseViewModel : BaseViewModel
    {
        private readonly IHouseService _houseService;
        private readonly IDialogService _dialogService;

        private int? _editingHouseId;
        private Guid? _editingHouseApiId;
        private string _name = string.Empty;
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

        /// <summary>Title for the dialog (e.g. "Add House" or "Edit House").</summary>
        public string DialogTitle => _editingHouseId.HasValue ? "Edit House" : "Add House";

        /// <summary>Load existing house for editing. Call before showing the dialog.</summary>
        public void LoadForEdit(House house)
        {
            if (house == null) return;
            _editingHouseId = house.HouseId;
            _editingHouseApiId = house.ApiId;
            Name = string.IsNullOrWhiteSpace(house.Name) ? house.AddressLine1 : house.Name;
            AddressLine1 = house.AddressLine1;
            AddressLine2 = house.AddressLine2;
            City = house.City;
            Postcode = house.Postcode;
            TotalRooms = house.TotalRooms > 0 ? house.TotalRooms : 1;
            OnPropertyChanged(nameof(DialogTitle));
        }

        public string Name
        {
            get => _name;
            set
            {
                SetProperty(ref _name, value ?? string.Empty);
                ((RelayCommand)SaveCommand).RaiseCanExecuteChanged();
            }
        }

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
                    Name = Name,
                    AddressLine1 = AddressLine1,
                    AddressLine2 = AddressLine2,
                    City = City,
                    Postcode = Postcode,
                    TotalRooms = TotalRooms
                };

                if (_editingHouseId.HasValue)
                {
                    house.HouseId = _editingHouseId.Value;
                    house.ApiId = _editingHouseApiId;
                    await _houseService.UpdateHouseAsync(house);
                    _dialogService.ShowMessage("House updated successfully!", "Success");
                }
                else
                {
                    await _houseService.CreateHouseAsync(house);
                    _dialogService.ShowMessage("House added successfully!", "Success");
                }
                CloseRequested?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error saving house: {ex.Message}", "Error");
            }
        }
    }
}
