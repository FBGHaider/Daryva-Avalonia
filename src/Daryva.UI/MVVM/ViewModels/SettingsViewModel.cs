using System.Windows.Input;
using Daryva.MVVM.Commands;
using Daryva.Services;
using Daryva.Services.Business;
using Daryva.Services.Dialog;
using Microsoft.Extensions.DependencyInjection;

namespace Daryva.MVVM.ViewModels
{
    public class SettingsViewModel : BaseViewModel
    {
        private readonly IServiceProvider _serviceProvider;
        private string _selectedSection = "General";
        private BaseViewModel? _currentSectionViewModel;

        public SettingsViewModel(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

            NavigateToSectionCommand = new RelayCommand<string>(section => NavigateToSection(section ?? "General"));

            // Initialize with General section
            NavigateToSection("General");
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
            "General",
            "Theme",
            "Advanced",
            "Rent & Finance",
            "Documents",
            "Notifications",
            "Email / Integrations",
            "Data & Backup"
        };

        private void NavigateToSection(string section)
        {
            try
            {
                CurrentSectionViewModel = section switch
                {
                    "General" => _serviceProvider.GetRequiredService<GeneralSettingsViewModel>(),
                    "Theme" => _serviceProvider.GetRequiredService<ThemeSettingsViewModel>(),
                    "Advanced" => _serviceProvider.GetRequiredService<AdvancedSettingsViewModel>(),
                    "API" => _serviceProvider.GetRequiredService<AdvancedSettingsViewModel>(),
                    "Database" => _serviceProvider.GetRequiredService<AdvancedSettingsViewModel>(),
                    "Rent & Finance" => _serviceProvider.GetRequiredService<RentSettingsViewModel>(),
                    "Documents" => _serviceProvider.GetRequiredService<DocumentSettingsViewModel>(),
                    "Notifications" => _serviceProvider.GetRequiredService<NotificationSettingsViewModel>(),
                    "Email / Integrations" => _serviceProvider.GetRequiredService<EmailSettingsViewModel>(),
                    "Data & Backup" => _serviceProvider.GetRequiredService<BackupSettingsViewModel>(),
                    _ => _serviceProvider.GetRequiredService<GeneralSettingsViewModel>()
                };
            }
            catch (Exception ex)
            {
                var dialogService = _serviceProvider.GetRequiredService<IDialogService>();
                dialogService.ShowMessage($"Error loading section: {ex.Message}", "Error");
            }
        }
    }
}
