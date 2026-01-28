using Avalonia.Controls;
using Avalonia.Interactivity;
using Daryva.MVVM.ViewModels;

namespace Daryva.MVVM.Views;

public partial class ThemeSettingsView : UserControl
{
    private bool _isUpdatingCheckStates = false;

    public ThemeSettingsView()
    {
        InitializeComponent();
        
        // Subscribe to ViewModel changes
        this.DataContextChanged += (s, e) =>
        {
            if (this.DataContext is ThemeSettingsViewModel vm)
            {
                UpdateRadioButtonStates(vm.SelectedTheme);
                vm.PropertyChanged += (s2, e2) =>
                {
                    if (e2.PropertyName == nameof(ThemeSettingsViewModel.SelectedTheme))
                    {
                        UpdateRadioButtonStates(vm.SelectedTheme);
                    }
                };
            }
        };
    }

    private void OnLightChecked(object? sender, RoutedEventArgs e)
    {
        if (_isUpdatingCheckStates || DataContext is not ThemeSettingsViewModel vm)
            return;
            
        if (vm.SelectedTheme != "Light")
        {
            vm.SelectedTheme = "Light";
        }
    }

    private void OnDarkChecked(object? sender, RoutedEventArgs e)
    {
        if (_isUpdatingCheckStates || DataContext is not ThemeSettingsViewModel vm)
            return;
            
        if (vm.SelectedTheme != "Dark")
        {
            vm.SelectedTheme = "Dark";
        }
    }

    private void UpdateRadioButtonStates(string selectedTheme)
    {
        _isUpdatingCheckStates = true;
        try
        {
            // Find the radio buttons and update their checked state
            var lightRb = this.FindControl<RadioButton>("LightRadioButton");
            var darkRb = this.FindControl<RadioButton>("DarkRadioButton");
            
            if (lightRb != null && darkRb != null)
            {
                lightRb.IsChecked = selectedTheme == "Light";
                darkRb.IsChecked = selectedTheme == "Dark";
            }
        }
        finally
        {
            _isUpdatingCheckStates = false;
        }
    }
}
