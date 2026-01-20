using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Daryva.MVVM.ViewModels;

namespace Daryva.MVVM.Views
{
    public partial class RecordPaymentDialog : Window
    {
        private RecordPaymentViewModel? _viewModel;

        public RecordPaymentDialog(RecordPaymentViewModel viewModel)
        {
            InitializeComponent();
            
            _viewModel = viewModel;
            DataContext = viewModel;
            
            ShowInTaskbar = false;
            Topmost = false;
            
            viewModel.CloseRequested += (s, e) => 
            {
                DialogResult = true;
                Close();
            };

            Loaded += async (s, e) =>
            {
                try
                {
                    RecordPaymentDialog_Loaded(s, e);
                    
                    if (_viewModel != null)
                    {
                        await _viewModel.InitializeAsync();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error loading tenancies: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };
        }

        private void RecordPaymentDialog_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Initialize month and year combos
                if (MonthComboBox != null && _viewModel != null && _viewModel.RentMonth >= 1 && _viewModel.RentMonth <= 12)
                {
                    MonthComboBox.SelectedIndex = _viewModel.RentMonth - 1;
                }

                if (YearComboBox != null && _viewModel != null)
                {
                    foreach (System.Windows.Controls.ComboBoxItem item in YearComboBox.Items)
                    {
                        if (item.Tag != null && int.TryParse(item.Tag.ToString(), out int year) && year == _viewModel.RentYear)
                        {
                            YearComboBox.SelectedItem = item;
                            break;
                        }
                    }
                }

                // Initialize payment method
                if (PaymentMethodComboBox != null && _viewModel != null)
                {
                    foreach (System.Windows.Controls.ComboBoxItem item in PaymentMethodComboBox.Items)
                    {
                        if (item.Tag is string method && method == _viewModel.PaymentMethod)
                        {
                            PaymentMethodComboBox.SelectedItem = item;
                            break;
                        }
                    }
                    // If no match found, select first item
                    if (PaymentMethodComboBox.SelectedItem == null && PaymentMethodComboBox.Items.Count > 0)
                    {
                        PaymentMethodComboBox.SelectedIndex = 0;
                    }
                }
            }
            catch
            {
                // Error initializing controls - ignore
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void MonthComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_viewModel != null && sender is System.Windows.Controls.ComboBox comboBox && comboBox.SelectedIndex >= 0)
            {
                _viewModel.RentMonth = comboBox.SelectedIndex + 1;
            }
        }

        private void PaymentMethodComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_viewModel != null && sender is System.Windows.Controls.ComboBox comboBox && comboBox.SelectedItem is System.Windows.Controls.ComboBoxItem item && item.Tag is string method)
            {
                _viewModel.PaymentMethod = method;
            }
        }

        private void YearComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_viewModel != null && sender is System.Windows.Controls.ComboBox comboBox && comboBox.SelectedItem is System.Windows.Controls.ComboBoxItem item && item.Tag is int year)
            {
                _viewModel.RentYear = year;
            }
        }
    }
}
