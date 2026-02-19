using System.Windows.Input;
using Daryva.MVVM.Commands;
using Daryva.Services.Dialog;
using Microsoft.Extensions.DependencyInjection;

namespace Daryva.MVVM.ViewModels;

public class AdvancedSettingsViewModel : BaseViewModel
{
    private readonly IServiceProvider _serviceProvider;
    private string _selectedSection = "API";
    private BaseViewModel? _currentSectionViewModel;

    public AdvancedSettingsViewModel(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

        NavigateToSectionCommand = new RelayCommand<string>(section => NavigateToSection(section ?? "API"));

        NavigateToSection("API");
    }

    public ICommand NavigateToSectionCommand { get; }

    public string SelectedSection
    {
        get => _selectedSection;
        set
        {
            if (SetProperty(ref _selectedSection, value))
            {
                NavigateToSection(value);
            }
        }
    }

    public BaseViewModel? CurrentSectionViewModel
    {
        get => _currentSectionViewModel;
        set => SetProperty(ref _currentSectionViewModel, value);
    }

    public List<string> Sections { get; } = new()
    {
        "API"
    };

    private void NavigateToSection(string section)
    {
        try
        {
            CurrentSectionViewModel = section switch
            {
                "API" => _serviceProvider.GetRequiredService<DatabaseSettingsViewModel>(),
                _ => _serviceProvider.GetRequiredService<DatabaseSettingsViewModel>()
            };
        }
        catch (Exception ex)
        {
            var dialogService = _serviceProvider.GetRequiredService<IDialogService>();
            dialogService.ShowMessage($"Error loading advanced section: {ex.Message}", "Error");
        }
    }
}
