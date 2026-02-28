using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;

namespace Daryva.MVVM.Views.Controls;

public partial class ProfileDrawer : UserControl
{
    public ProfileDrawer()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is ViewModels.ProfileMenuViewModel vm)
        {
            vm.PropertyChanged += ProfileMenu_PropertyChanged;
        }
    }

    private void ProfileMenu_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ViewModels.ProfileMenuViewModel.IsOpen) &&
            DataContext is ViewModels.ProfileMenuViewModel vm &&
            vm.IsOpen)
        {
            Focus();
        }
    }

    private void Overlay_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is ViewModels.ProfileMenuViewModel vm)
        {
            vm.Close();
        }
    }
}
