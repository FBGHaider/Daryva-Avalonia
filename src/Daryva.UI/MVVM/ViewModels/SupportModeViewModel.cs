using System.Collections.ObjectModel;
using System.Windows.Input;
using Daryva.MVVM.Commands;
using Daryva.Services.Api;
using Daryva.Services.Dialog;
using Daryva.Services.Navigation;
using Daryva.Services.OrgContext;

namespace Daryva.MVVM.ViewModels
{
    /// <summary>
    /// Platform-admin Support Mode: search for an organization (not membership-scoped -- an admin
    /// needs no memberships of their own), start a time-boxed audited Support Session on it, and
    /// manage active/past sessions. Every backend call here requires AppUser.IsPlatformAdmin; a
    /// non-admin who somehow lands here gets the server's 403 surfaced as a normal ErrorMessage,
    /// same pattern as the Audit Log page.
    /// </summary>
    public class SupportModeViewModel : BaseViewModel
    {
        private readonly ISupportSessionApiService _supportSessionApiService;
        private readonly IPlatformAdminApiService _platformAdminApiService;
        private readonly IDialogService _dialogService;
        private readonly IOrgContext _orgContext;
        private readonly INavigationService _navigationService;
        private readonly IAuthSessionService _authSessionService;

        private bool _isBusy;
        private string _errorMessage = string.Empty;
        private string _orgSearchText = string.Empty;
        private bool _hasSearched;
        private AdminMemberSearchResultDto? _selectedMember;
        private AdminOrganizationSummaryDto? _selectedOrg;
        private string _reason = string.Empty;
        private string _accessCode = string.Empty;

        public SupportModeViewModel(
            ISupportSessionApiService supportSessionApiService,
            IPlatformAdminApiService platformAdminApiService,
            IDialogService dialogService,
            IOrgContext orgContext,
            INavigationService navigationService,
            IAuthSessionService authSessionService)
        {
            _supportSessionApiService = supportSessionApiService ?? throw new ArgumentNullException(nameof(supportSessionApiService));
            _platformAdminApiService = platformAdminApiService ?? throw new ArgumentNullException(nameof(platformAdminApiService));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _orgContext = orgContext ?? throw new ArgumentNullException(nameof(orgContext));
            _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
            _authSessionService = authSessionService ?? throw new ArgumentNullException(nameof(authSessionService));

            OrgSearchResults = new ObservableCollection<AdminMemberSearchResultDto>();
            ActiveSessions = new ObservableCollection<SupportSessionVm>();
            PastSessions = new ObservableCollection<SupportSessionVm>();
            PlatformAdmins = new ObservableCollection<PlatformAdminVm>();

            SearchOrgsCommand = new RelayCommand(async _ => await SearchOrgsAsync());
            SelectMemberCommand = new RelayCommand<AdminMemberSearchResultDto>(m => SelectMember(m));
            ClearSelectedMemberCommand = new RelayCommand(_ => SelectedMember = null);
            SelectOrgCommand = new RelayCommand<AdminOrganizationSummaryDto>(org => SelectOrg(org));
            ClearSelectedOrgCommand = new RelayCommand(_ => SelectedOrg = null);
            ResolveCodeCommand = new RelayCommand(async _ => await ResolveCodeAsync(), _ => !IsBusy && AccessCode.Trim().Length >= 4);
            StartSessionCommand = new RelayCommand(async _ => await StartSessionAsync(), _ => CanStartSession());
            EndSessionCommand = new RelayCommand<SupportSessionVm>(async s => await EndSessionAsync(s));
            RefreshSessionsCommand = new RelayCommand(async _ => await LoadSessionsAsync());
            EnterOrganizationCommand = new RelayCommand<SupportSessionVm>(async s => await EnterOrganizationAsync(s));
            RefreshPlatformAdminsCommand = new RelayCommand(async _ => await LoadPlatformAdminsAsync());
            RevokePlatformAdminCommand = new RelayCommand<PlatformAdminVm>(async a => await RevokePlatformAdminAsync(a));

            _ = LoadSessionsAsync();
            _ = LoadPlatformAdminsAsync();
        }

        public string PageTitle => "Support Mode";
        public string Subtitle => "Find the landlord by email, or enter their support code, to start a time-boxed, audited support session.";

        public ObservableCollection<AdminMemberSearchResultDto> OrgSearchResults { get; }
        public ObservableCollection<SupportSessionVm> ActiveSessions { get; }
        public ObservableCollection<SupportSessionVm> PastSessions { get; }
        public ObservableCollection<PlatformAdminVm> PlatformAdmins { get; }

        public ICommand SearchOrgsCommand { get; }
        public ICommand SelectMemberCommand { get; }
        public ICommand ClearSelectedMemberCommand { get; }
        public ICommand SelectOrgCommand { get; }
        public ICommand ClearSelectedOrgCommand { get; }
        public RelayCommand ResolveCodeCommand { get; }
        public RelayCommand StartSessionCommand { get; }
        public ICommand EndSessionCommand { get; }
        public ICommand RefreshSessionsCommand { get; }
        public ICommand EnterOrganizationCommand { get; }
        public ICommand RefreshPlatformAdminsCommand { get; }
        public ICommand RevokePlatformAdminCommand { get; }

        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                if (SetProperty(ref _isBusy, value))
                {
                    StartSessionCommand.RaiseCanExecuteChanged();
                    ResolveCodeCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set
            {
                if (SetProperty(ref _errorMessage, value ?? string.Empty))
                    OnPropertyChanged(nameof(HasErrorMessage));
            }
        }

        public bool HasErrorMessage => !string.IsNullOrWhiteSpace(ErrorMessage);

        public string OrgSearchText
        {
            get => _orgSearchText;
            set => SetProperty(ref _orgSearchText, value ?? string.Empty);
        }

        public bool HasSearched
        {
            get => _hasSearched;
            private set => SetProperty(ref _hasSearched, value);
        }

        public bool HasNoSearchResults => HasSearched && OrgSearchResults.Count == 0;

        /// <summary>Step 1 result: the landlord picked by email. Null while still browsing search
        /// results. Its Organizations list drives step 2 (pick which org to act on).</summary>
        public AdminMemberSearchResultDto? SelectedMember
        {
            get => _selectedMember;
            private set
            {
                if (SetProperty(ref _selectedMember, value))
                    OnPropertyChanged(nameof(HasSelectedMember));
            }
        }

        public bool HasSelectedMember => SelectedMember != null;

        public AdminOrganizationSummaryDto? SelectedOrg
        {
            get => _selectedOrg;
            private set
            {
                if (SetProperty(ref _selectedOrg, value))
                {
                    OnPropertyChanged(nameof(HasSelectedOrg));
                    StartSessionCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public bool HasSelectedOrg => SelectedOrg != null;

        public string Reason
        {
            get => _reason;
            set
            {
                if (SetProperty(ref _reason, value ?? string.Empty))
                    StartSessionCommand.RaiseCanExecuteChanged();
            }
        }

        public string AccessCode
        {
            get => _accessCode;
            set
            {
                if (SetProperty(ref _accessCode, value ?? string.Empty))
                    ResolveCodeCommand.RaiseCanExecuteChanged();
            }
        }

        public bool HasActiveSessions => ActiveSessions.Count > 0;
        public bool HasPastSessions => PastSessions.Count > 0;
        public bool HasPlatformAdmins => PlatformAdmins.Count > 0;

        private void SelectMember(AdminMemberSearchResultDto? member)
        {
            if (member == null)
                return;
            SelectedMember = member;
            ErrorMessage = string.Empty;

            // Skip the org-picker step entirely when the landlord has exactly one org -- the common
            // case -- so the admin doesn't have to click through a list of one.
            if (member.Organizations.Count == 1)
                SelectOrg(member.Organizations[0]);
        }

        private void SelectOrg(AdminOrganizationSummaryDto? org)
        {
            if (org == null)
                return;
            SelectedOrg = org;
            ErrorMessage = string.Empty;
        }

        private bool CanStartSession()
            => !IsBusy && SelectedOrg != null && !string.IsNullOrWhiteSpace(Reason);

        private async Task SearchOrgsAsync()
        {
            if (IsBusy)
                return;

            // An empty search must not fall back to browsing every organization on the platform --
            // the backend already refuses to (SearchOrganizationsByEmailAsync), but bail out here
            // too so a blank Search click doesn't even round-trip, and clears any stale results.
            if (string.IsNullOrWhiteSpace(OrgSearchText))
            {
                OrgSearchResults.Clear();
                HasSearched = false;
                ErrorMessage = string.Empty;
                OnPropertyChanged(nameof(HasNoSearchResults));
                return;
            }

            IsBusy = true;
            ErrorMessage = string.Empty;
            SelectedMember = null;
            SelectedOrg = null;
            try
            {
                var result = await _supportSessionApiService.SearchOrganizationsByEmailAsync(OrgSearchText.Trim()).ConfigureAwait(true);
                OrgSearchResults.Clear();
                foreach (var match in result.Matches)
                    OrgSearchResults.Add(match);
                HasSearched = true;
                OnPropertyChanged(nameof(HasNoSearchResults));

                // Exactly one landlord matched -- skip straight to their org picker (or further, to
                // the single-org skip in SelectMember) instead of making the admin click "Select"
                // on a list that only has one row.
                if (OrgSearchResults.Count == 1)
                    SelectMember(OrgSearchResults[0]);
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task ResolveCodeAsync()
        {
            if (IsBusy || AccessCode.Trim().Length < 4)
                return;

            IsBusy = true;
            ErrorMessage = string.Empty;
            try
            {
                var resolved = await _supportSessionApiService.ResolveAccessCodeAsync(AccessCode.Trim()).ConfigureAwait(true);
                if (resolved == null)
                {
                    ErrorMessage = "That code is invalid, expired, or has already been used.";
                    return;
                }

                AccessCode = string.Empty;
                SelectedMember = null;
                OrgSearchResults.Clear();
                OrgSearchText = string.Empty;
                HasSearched = false;
                OnPropertyChanged(nameof(HasNoSearchResults));
                SelectOrg(new AdminOrganizationSummaryDto { Id = resolved.OrganizationId, Name = resolved.OrganizationName });
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task StartSessionAsync()
        {
            // Guard set before the confirmation dialog (not just checked at the top): CanStartSession()
            // is only re-consulted by the UI when RaiseCanExecuteChanged fires, so a second click while
            // the first click's dialog is still open would otherwise re-enter this method and, if
            // confirmed twice, start two real sessions -- exactly what happened during testing.
            if (IsBusy || !CanStartSession() || SelectedOrg == null)
                return;

            IsBusy = true;
            try
            {
                var confirmed = await _dialogService.ShowConfirmationAsync(
                    $"Start a support session on \"{SelectedOrg.Name}\"? The organization's Landlord will be notified by email, and every action you take will be recorded in their audit log.",
                    "Start support session");
                if (!confirmed)
                    return;

                ErrorMessage = string.Empty;
                await _supportSessionApiService.StartSessionAsync(SelectedOrg.Id, Reason.Trim()).ConfigureAwait(true);
                Reason = string.Empty;
                SelectedOrg = null;
                SelectedMember = null;
                OrgSearchResults.Clear();
                OrgSearchText = string.Empty;
                HasSearched = false;
                OnPropertyChanged(nameof(HasNoSearchResults));
                await RefreshSessionsCoreAsync().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task EndSessionAsync(SupportSessionVm? session)
        {
            if (session == null || IsBusy)
                return;

            IsBusy = true;
            try
            {
                var confirmed = await _dialogService.ShowConfirmationAsync(
                    $"End the support session on \"{session.OrganizationName}\"?",
                    "End support session");
                if (!confirmed)
                    return;

                ErrorMessage = string.Empty;
                await _supportSessionApiService.EndSessionAsync(session.Id).ConfigureAwait(true);
                await RefreshSessionsCoreAsync().ConfigureAwait(true);

                // If this is the org the admin is currently "inside" via EnterSupportOrgAsync, the
                // client's org context now points at an org with no active session -- every subsequent
                // request from any open tab would 403 with a raw "not a member" error until something
                // reverts it. Drop back to the admin's real orgs immediately instead of leaving that
                // for the admin to discover the hard way.
                if (_orgContext.ActiveSupportSession?.SessionId == session.Id)
                    await _orgContext.ExitSupportOrgAsync().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task EnterOrganizationAsync(SupportSessionVm? session)
        {
            if (session == null || !session.IsActive || IsBusy)
                return;

            await _orgContext.EnterSupportOrgAsync(session.OrganizationId, session.OrganizationName, session.Id, session.ExpiresAtUtc).ConfigureAwait(true);
            _navigationService.NavigateTo<DashboardViewModel>();
        }

        private async Task LoadSessionsAsync()
        {
            if (IsBusy)
                return;

            IsBusy = true;
            ErrorMessage = string.Empty;
            try
            {
                await RefreshSessionsCoreAsync().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// The actual fetch-and-repopulate logic, with no IsBusy guard of its own -- callers that are
        /// already inside their own IsBusy=true block (StartSessionAsync, EndSessionAsync) call this
        /// directly. LoadSessionsAsync's "if (IsBusy) return" guard previously made it a silent no-op
        /// whenever it was called from inside Start/End (IsBusy was still true at that point), which is
        /// why the session list never visibly updated after starting or ending one.
        /// </summary>
        private async Task RefreshSessionsCoreAsync()
        {
            var sessions = await _supportSessionApiService.ListSessionsAsync(includeEnded: true).ConfigureAwait(true);

            ActiveSessions.Clear();
            PastSessions.Clear();
            foreach (var dto in sessions.OrderByDescending(s => s.StartedAt))
            {
                var vm = SupportSessionVm.FromDto(dto);
                if (vm.IsActive)
                    ActiveSessions.Add(vm);
                else
                    PastSessions.Add(vm);
            }
            OnPropertyChanged(nameof(HasActiveSessions));
            OnPropertyChanged(nameof(HasPastSessions));
        }

        private async Task LoadPlatformAdminsAsync()
        {
            if (IsBusy)
                return;

            IsBusy = true;
            ErrorMessage = string.Empty;
            try
            {
                var admins = await _platformAdminApiService.ListAsync().ConfigureAwait(true);
                var selfId = _authSessionService.UserId;

                PlatformAdmins.Clear();
                foreach (var dto in admins)
                    PlatformAdmins.Add(PlatformAdminVm.FromDto(dto, selfId));
                OnPropertyChanged(nameof(HasPlatformAdmins));
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task RevokePlatformAdminAsync(PlatformAdminVm? admin)
        {
            if (admin == null || !admin.CanRevoke || IsBusy)
                return;

            IsBusy = true;
            try
            {
                var confirmed = await _dialogService.ShowConfirmationAsync(
                    $"Revoke platform admin access from \"{admin.Email}\"? They will immediately lose all platform-admin permissions.",
                    "Revoke admin access");
                if (!confirmed)
                    return;

                ErrorMessage = string.Empty;
                await _platformAdminApiService.RevokeAsync(admin.Id).ConfigureAwait(true);

                var selfId = _authSessionService.UserId;
                var admins = await _platformAdminApiService.ListAsync().ConfigureAwait(true);
                PlatformAdmins.Clear();
                foreach (var dto in admins)
                    PlatformAdmins.Add(PlatformAdminVm.FromDto(dto, selfId));
                OnPropertyChanged(nameof(HasPlatformAdmins));
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
