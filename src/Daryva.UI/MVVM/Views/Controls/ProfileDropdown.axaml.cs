using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;

namespace Daryva.MVVM.Views.Controls;

public partial class ProfileDropdown : UserControl
{
    public ProfileDropdown()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void Overlay_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is ViewModels.ProfileMenuViewModel vm)
        {
            vm.Close();
        }
    }
}
