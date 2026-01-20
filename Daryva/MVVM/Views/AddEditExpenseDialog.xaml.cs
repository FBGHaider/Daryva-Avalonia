using System.Windows;
using Daryva.MVVM.ViewModels;

namespace Daryva.MVVM.Views
{
    public partial class AddEditExpenseDialog : Window
    {
        public AddEditExpenseDialog(AddEditExpenseViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            
            if (viewModel != null)
            {
                // Update window title based on edit mode
                Title = viewModel.IsEditMode ? "Edit Expense" : "Add Expense";
                
                viewModel.CloseRequested += (sender, result) =>
                {
                    DialogResult = result;
                    Close();
                };
            }
        }
    }
}
