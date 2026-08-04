using System.Windows.Input;
using Daryva.MVVM.Commands;
using Daryva.Services.Business;
using Daryva.Services.OrgContext;
using Microsoft.Extensions.DependencyInjection;

namespace Daryva.MVVM.ViewModels
{
    /// <summary>
    /// Account page: profile, security, and notification preferences (SaaS-style My Account).
    /// Account is tied to the signed-in identity (the admin), never the org being supported --
    /// so during a Support Session every section here is locked (see IsRestrictedBySupportSession):
    /// letting an admin edit the landlord's password/2FA/notification prefs under their own login
    /// would be a real privacy/security problem, not a feature. The landlord's email is shown for
    /// context only, read from the org's member list, not swapped in as if it were the signed-in user.
    /// </summary>
    public class AccountViewModel : BaseViewModel
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IOrgContext _orgContext;
        private readonly IOrganisationMemberService _memberService;
        private string _selectedSection = "Profile";
        private BaseViewModel? _currentSectionViewModel;
        private string _supportSessionOwnerEmail = string.Empty;

        public AccountViewModel(IServiceProvider serviceProvider, IOrgContext orgContext, IOrganisationMemberService memberService)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _orgContext = orgContext ?? throw new ArgumentNullException(nameof(orgContext));
            _memberService = memberService ?? throw new ArgumentNullException(nameof(memberService));

            NavigateToSectionCommand = new RelayCommand<string>(section => NavigateToSection(section ?? "Profile"));
            NavigateToSection("Profile");

            if (IsRestrictedBySupportSession)
                _ = LoadSupportSessionOwnerEmailAsync();
        }

        public ICommand NavigateToSectionCommand { get; }

        /// <summary>Whether the account management sections should be locked -- true whenever acting
        /// inside an org entered via Support Mode.</summary>
        public bool IsRestrictedBySupportSession => _orgContext.ActiveSupportSession != null;

        /// <summary>The org's primary owner email, for context while locked ("you're supporting
        /// this landlord's org") -- not a stand-in for the signed-in admin's own email.</summary>
        public string SupportSessionOwnerEmail
        {
            get => _supportSessionOwnerEmail;
            private set => SetProperty(ref _supportSessionOwnerEmail, value);
        }

        private async Task LoadSupportSessionOwnerEmailAsync()
        {
            var active = _orgContext.ActiveSupportSession;
            if (active == null)
                return;
            try
            {
                var members = await _memberService.GetMembersAsync(active.OrganizationId).ConfigureAwait(false);
                var owner = members.FirstOrDefault(m => m.IsPrimaryOwner) ?? members.FirstOrDefault();
                if (owner != null)
                    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => SupportSessionOwnerEmail = owner.Email);
            }
            catch { /* best-effort context label; the lock itself doesn't depend on this */ }
        }

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
