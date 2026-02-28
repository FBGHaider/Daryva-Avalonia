using System.Windows.Input;
using Daryva.MVVM.Commands;
using Microsoft.Extensions.DependencyInjection;

namespace Daryva.MVVM.ViewModels
{
    /// <summary>
    /// Account page: profile, security, and notification preferences (SaaS-style My Account).
    /// </summary>
    public class AccountViewModel : BaseViewModel
    {
        private readonly IServiceProvider _serviceProvider;
        private string _selectedSection = "Profile";
        private BaseViewModel? _currentSectionViewModel;

        public AccountViewModel(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

            NavigateToSectionCommand = new RelayCommand<string>(section => NavigateToSection(section ?? "Profile"));
            NavigateToSection("Profile");
        }

        public ICommand NavigateToSectionCommand { get; }

        public string SelectedSection
        {
            get => _selectedSection;
            set
            {
                if (SetProperty(ref _selectedSection, value))
                    NavigateToSection(value);
            }
        }

        public BaseViewModel? CurrentSectionViewModel
        {
            get => _currentSectionViewModel;
            set => SetProperty(ref _currentSectionViewModel, value);
        }

        public List<string> Sections { get; } = new() { "Profile", "Security", "Notifications" };

        private void NavigateToSection(string section)
        {
            try
            {
                CurrentSectionViewModel = section switch
                {
                    "Profile" => _serviceProvider.GetRequiredService<AccountProfileSectionViewModel>(),
                    "Security" => _serviceProvider.GetRequiredService<AccountSecuritySectionViewModel>(),
                    "Notifications" => _serviceProvider.GetRequiredService<AccountNotificationsSectionViewModel>(),
                    _ => _serviceProvider.GetRequiredService<AccountProfileSectionViewModel>()
                };
            }
            catch (Exception)
            {
                CurrentSectionViewModel = null;
            }
        }
    }
}
