using System.Windows;
using LandLordBuddy.MVVM.ViewModels;

namespace LandLordBuddy.MVVM.Views
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
