using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace Daryva.MVVM.Views.Controls;

public partial class NotificationDrawer : UserControl
{
    public NotificationDrawer()
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
        if (DataContext is ViewModels.NotificationCenterViewModel vm)
        {
            vm.PropertyChanged += NotificationCenter_PropertyChanged;
        }
    }

    private void NotificationCenter_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ViewModels.NotificationCenterViewModel.IsOpen) &&
            DataContext is ViewModels.NotificationCenterViewModel vm &&
            vm.IsOpen)
        {
            Focus();
        }
    }

    private void Overlay_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is ViewModels.NotificationCenterViewModel vm)
        {
            vm.Close();
        }
    }
}
