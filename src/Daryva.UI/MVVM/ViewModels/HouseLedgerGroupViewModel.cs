using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Daryva.MVVM.Models;

namespace Daryva.MVVM.ViewModels
{
    /// <summary>
    /// Groups rent and deposit ledger rows by house for expandable house rows in the Rent &amp; Payments tab.
    /// </summary>
    public class HouseLedgerGroupViewModel : INotifyPropertyChanged
    {
        private bool _isExpanded = false;

        public int HouseId { get; set; }
        public string HouseAddress { get; set; } = string.Empty;
        public List<RentLedgerRowViewModel> RentRows { get; } = new List<RentLedgerRowViewModel>();
        public List<DepositLedgerRowViewModel> DepositRows { get; } = new List<DepositLedgerRowViewModel>();

        public int TenantCount => RentRows.Count;
        public decimal TotalDue => RentRows.Sum(r => r.AmountDue);
        public decimal TotalPaid => RentRows.Sum(r => r.AmountPaid);
        public decimal TotalBalance => RentRows.Sum(r => r.Balance);
        public int OverdueCount => RentRows.Count(r => string.Equals(r.Status, "Overdue", StringComparison.OrdinalIgnoreCase));

        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded == value) return;
                _isExpanded = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
