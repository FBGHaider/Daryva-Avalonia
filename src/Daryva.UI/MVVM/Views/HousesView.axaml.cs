using Avalonia.Controls;
using Avalonia.Input;
using Daryva.MVVM.ViewModels;

namespace Daryva.MVVM.Views;

public partial class HousesView : UserControl
{
    public HousesView()
    {
        InitializeComponent();
    }

    private void HousesGrid_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is not HousesViewModel vm || vm.SelectedHouse == null)
            return;
        vm.OpenSelectedHouseCommand.Execute(null);
    }
}
