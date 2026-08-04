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
        private readonly IDialogService _dialogService;
        private readonly IOrgContext _orgContext;
        private readonly INavigationService _navigationService;

        private bool _isBusy;
        private string _errorMessage = string.Empty;
        private string _orgSearchText = string.Empty;
        private bool _hasSearched;
        private AdminOrganizationSummaryDto? _selectedOrg;
        private string _reason = string.Empty;

        public SupportModeViewModel(
            ISupportSessionApiService supportSessionApiService,
            IDialogService dialogService,
            IOrgContext orgContext,
            INavigationService navigationService)
        {
            _supportSessionApiService = supportSessionApiService ?? throw new ArgumentNullException(nameof(supportSessionApiService));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _orgContext = orgContext ?? throw new ArgumentNullException(nameof(orgContext));
            _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));

            OrgSearchResults = new ObservableCollection<AdminOrganizationSummaryDto>();
            ActiveSessions = new ObservableCollection<SupportSessionVm>();
            PastSessions = new ObservableCollection<SupportSessionVm>();

            SearchOrgsCommand = new RelayCommand(async _ => await SearchOrgsAsync());
            SelectOrgCommand = new RelayCommand<AdminOrganizationSummaryDto>(org => SelectOrg(org));
            ClearSelectedOrgCommand = new RelayCommand(_ => SelectedOrg = null);
            StartSessionCommand = new RelayCommand(async _ => await StartSessionAsync(), _ => CanStartSession());
            EndSessionCommand = new RelayCommand<SupportSessionVm>(async s => await EndSessionAsync(s));
            RefreshSessionsCommand = new RelayCommand(async _ => await LoadSessionsAsync());
            EnterOrganizationCommand = new RelayCommand<SupportSessionVm>(async s => await EnterOrganizationAsync(s));

            _ = LoadSessionsAsync();
        }

        public string PageTitle => "Support Mode";
        public string Subtitle => "Search for an organization to start a time-boxed, audited support session.";

        public ObservableCollection<AdminOrganizationSummaryDto> OrgSearchResults { get; }
        public ObservableCollection<SupportSessionVm> ActiveSessions { get; }
        public ObservableCollection<SupportSessionVm> PastSessions { get; }

        public ICommand SearchOrgsCommand { get; }
        public ICommand SelectOrgCommand { get; }
        public ICommand ClearSelectedOrgCommand { get; }
        public RelayCommand StartSessionCommand { get; }
        public ICommand EndSessionCommand { get; }
        public ICommand RefreshSessionsCommand { get; }
        public ICommand EnterOrganizationCommand { get; }

        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
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

        public bool HasActiveSessions => ActiveSessions.Count > 0;
        public bool HasPastSessions => PastSessions.Count > 0;

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

            IsBusy = true;
            ErrorMessage = string.Empty;
            try
            {
                var result = await _supportSessionApiService.GetAllOrganizationsAsync(OrgSearchText.Trim()).ConfigureAwait(true);
                OrgSearchResults.Clear();
                foreach (var org in result.Items)
                    OrgSearchResults.Add(org);
                HasSearched = true;
                OnPropertyChanged(nameof(HasNoSearchResults));
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
            if (!CanStartSession() || SelectedOrg == null)
                return;

            var confirmed = await _dialogService.ShowConfirmationAsync(
                $"Start a support session on \"{SelectedOrg.Name}\"? The organization's Landlord will be notified by email, and every action you take will be recorded in their audit log.",
                "Start support session");
            if (!confirmed)
                return;

            IsBusy = true;
            ErrorMessage = string.Empty;
            try
            {
                await _supportSessionApiService.StartSessionAsync(SelectedOrg.Id, Reason.Trim()).ConfigureAwait(true);
                Reason = string.Empty;
                SelectedOrg = null;
                OrgSearchResults.Clear();
                OrgSearchText = string.Empty;
                HasSearched = false;
                OnPropertyChanged(nameof(HasNoSearchResults));
                await LoadSessionsAsync().ConfigureAwait(true);
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
            if (session == null)
                return;

            var confirmed = await _dialogService.ShowConfirmationAsync(
                $"End the support session on \"{session.OrganizationName}\"?",
                "End support session");
            if (!confirmed)
                return;

            IsBusy = true;
            ErrorMessage = string.Empty;
            try
            {
                await _supportSessionApiService.EndSessionAsync(session.Id).ConfigureAwait(true);
                await LoadSessionsAsync().ConfigureAwait(true);
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
            if (session == null || !session.IsActive)
                return;

            await _orgContext.EnterSupportOrgAsync(session.OrganizationId, session.OrganizationName).ConfigureAwait(true);
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
