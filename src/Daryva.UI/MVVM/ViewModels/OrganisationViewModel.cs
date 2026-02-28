using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia.Threading;
using Daryva.MVVM.Commands;
using Daryva.MVVM.Models;
using Daryva.Services.Api;
using Daryva.Services.Business;
using Daryva.Services.Dialog;
using Daryva.Services.OrgContext;

namespace Daryva.MVVM.ViewModels
{
    /// <summary>
    /// Organisation / Team page: manage organisations and team members (SaaS-style MVP).
    /// </summary>
    public class OrganisationViewModel : BaseViewModel
    {
        private readonly IOrganisationService _orgService;
        private readonly IOrgContext _orgContext;
        private readonly IOrganisationMemberService _memberService;
        private readonly IHouseService _houseService;
        private readonly IDialogService _dialogService;
        private readonly IAuthSessionService _authSession;

        private Organisation? _currentOrganisation;
        private Organisation? _selectedOrganisation;
        private OrgRole? _currentUserRole;
        private string _memberSearchText = string.Empty;
        private string _inviteEmail = string.Empty;
        private OrgRole _inviteRole = OrgRole.Member;
        private int _housesCount;
        private int _activeTenantsCount;
        private bool _isBusy;
        private DispatcherTimer? _searchDebounceTimer;
        private List<MemberVm> _allMembers = new();

        public OrganisationViewModel(
            IOrganisationService orgService,
            IOrgContext orgContext,
            IOrganisationMemberService memberService,
            IHouseService houseService,
            IDialogService dialogService,
            IAuthSessionService authSession)
        {
            _orgService = orgService ?? throw new ArgumentNullException(nameof(orgService));
            _orgContext = orgContext ?? throw new ArgumentNullException(nameof(orgContext));
            _memberService = memberService ?? throw new ArgumentNullException(nameof(memberService));
            _houseService = houseService ?? throw new ArgumentNullException(nameof(houseService));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _authSession = authSession ?? throw new ArgumentNullException(nameof(authSession));

            Organisations = new ObservableCollection<Organisation>();
            Members = new ObservableCollection<MemberVm>();
            RoleOptions = new ObservableCollection<OrgRole> { OrgRole.Owner, OrgRole.Admin, OrgRole.Member, OrgRole.ReadOnly };

            LoadCommand = new RelayCommand(async _ => await LoadAsync());
            RefreshCommand = new RelayCommand(async _ => await LoadAsync());
            SwitchOrgCommand = new RelayCommand(async _ => await SwitchOrgAsync(), _ => SelectedOrganisation != null && SelectedOrganisation.Id != CurrentOrganisation?.Id);
            CreateOrgCommand = new RelayCommand(async _ => await CreateOrgAsync());
            RenameOrgCommand = new RelayCommand(async _ => await RenameOrgAsync(), _ => CanRenameOrg);
            InviteMemberCommand = new RelayCommand(async _ => await InviteMemberAsync(), _ => CanInvite && !string.IsNullOrWhiteSpace(InviteEmail));
            ChangeRoleCommand = new RelayCommand<MemberVm>(async m => await ChangeRoleAsync(m), m => CanChangeRoleFor(m));
            RemoveMemberCommand = new RelayCommand<MemberVm>(async m => await RemoveMemberAsync(m), m => CanRemoveMember(m));

            _orgContext.CurrentOrgChanged += OnCurrentOrgChanged;

            _ = LoadAsync();
        }

        private void OnCurrentOrgChanged(object? sender, CurrentOrgChangedEventArgs e)
        {
            Dispatcher.UIThread.Post(() => _ = LoadAsync());
        }

        public string PageTitle => "Organisation";
        public string Subtitle => "Manage your organisation and team members.";

        public ObservableCollection<Organisation> Organisations { get; }
        public ObservableCollection<MemberVm> Members { get; }
        public ObservableCollection<OrgRole> RoleOptions { get; }

        public Organisation? CurrentOrganisation
        {
            get => _currentOrganisation;
            private set
            {
                if (SetProperty(ref _currentOrganisation, value))
                {
                    OnPropertyChanged(nameof(CurrentOrganisationName));
                    OnPropertyChanged(nameof(CanRenameOrg));
                    OnPropertyChanged(nameof(CanInvite));
                    ((RelayCommand)SwitchOrgCommand).RaiseCanExecuteChanged();
                    ((RelayCommand)RenameOrgCommand).RaiseCanExecuteChanged();
                    ((RelayCommand)InviteMemberCommand).RaiseCanExecuteChanged();
                }
            }
        }

        public Organisation? SelectedOrganisation
        {
            get => _selectedOrganisation;
            set
            {
                if (SetProperty(ref _selectedOrganisation, value))
                    ((RelayCommand)SwitchOrgCommand).RaiseCanExecuteChanged();
            }
        }

        public string CurrentOrganisationName => CurrentOrganisation?.Name ?? "(No organisation)";

        public string MemberSearchText
        {
            get => _memberSearchText;
            set
            {
                if (!SetProperty(ref _memberSearchText, value ?? string.Empty)) return;
                DebounceMemberSearch();
            }
        }

        public string InviteEmail
        {
            get => _inviteEmail;
            set
            {
                if (SetProperty(ref _inviteEmail, value ?? string.Empty))
                    ((RelayCommand)InviteMemberCommand).RaiseCanExecuteChanged();
            }
        }

        public OrgRole InviteRole
        {
            get => _inviteRole;
            set => SetProperty(ref _inviteRole, value);
        }

        public int HousesCount
        {
            get => _housesCount;
            private set => SetProperty(ref _housesCount, value);
        }

        public int ActiveTenantsCount
        {
            get => _activeTenantsCount;
            private set => SetProperty(ref _activeTenantsCount, value);
        }

        public bool IsBusy
        {
            get => _isBusy;
            private set => SetProperty(ref _isBusy, value);
        }

        public OrgRole? CurrentUserRole
        {
            get => _currentUserRole;
            private set
            {
                if (SetProperty(ref _currentUserRole, value))
                {
                    OnPropertyChanged(nameof(CanRenameOrg));
                    OnPropertyChanged(nameof(CanInvite));
                    ((RelayCommand)RenameOrgCommand).RaiseCanExecuteChanged();
                    ((RelayCommand)InviteMemberCommand).RaiseCanExecuteChanged();
                    foreach (var m in Members)
                    {
                        ((RelayCommand)ChangeRoleCommand).RaiseCanExecuteChanged();
                        ((RelayCommand)RemoveMemberCommand).RaiseCanExecuteChanged();
                    }
                }
            }
        }

        public bool CanRenameOrg => CurrentUserRole == OrgRole.Owner;
        public bool CanInvite => CurrentUserRole == OrgRole.Owner || CurrentUserRole == OrgRole.Admin;
        public bool IsMembersListEmpty => Members.Count == 0;
        public bool CanChangeRoleFor(MemberVm? m) => CurrentUserRole == OrgRole.Owner && m != null && m.Role != OrgRole.Owner && m.Member.Id != default;
        public bool CanRemoveMember(MemberVm? m) => (CurrentUserRole == OrgRole.Owner || CurrentUserRole == OrgRole.Admin) && m != null && (CurrentUserRole == OrgRole.Owner || m.Role != OrgRole.Owner);

        public ICommand LoadCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand SwitchOrgCommand { get; }
        public ICommand CreateOrgCommand { get; }
        public ICommand RenameOrgCommand { get; }
        public ICommand InviteMemberCommand { get; }
        public ICommand ChangeRoleCommand { get; }
        public ICommand RemoveMemberCommand { get; }

        private void DebounceMemberSearch()
        {
            _searchDebounceTimer?.Stop();
            _searchDebounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(250)
            };
            _searchDebounceTimer.Tick += (_, _) =>
            {
                _searchDebounceTimer?.Stop();
                _searchDebounceTimer = null;
                ApplyMemberFilter();
            };
            _searchDebounceTimer.Start();
        }

        private void ApplyMemberFilter()
        {
            var term = (MemberSearchText ?? string.Empty).Trim().ToLowerInvariant();
            Members.Clear();
            var source = string.IsNullOrEmpty(term)
                ? _allMembers
                : _allMembers.Where(m =>
                    (m.Email?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (m.DisplayName?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)).ToList();
            foreach (var m in source)
                Members.Add(m);
            OnPropertyChanged(nameof(IsMembersListEmpty));
        }

        private async Task LoadAsync()
        {
            if (IsBusy) return;
            IsBusy = true;
            try
            {
                var orgs = await _orgService.GetMyOrganisationsAsync();
                var currentId = await _orgService.GetCurrentOrganisationIdAsync();

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    Organisations.Clear();
                    foreach (var o in orgs)
                        Organisations.Add(o);
                    CurrentOrganisation = orgs.FirstOrDefault(o => o.Id == currentId);
                    SelectedOrganisation = CurrentOrganisation;
                });

                if (CurrentOrganisation != null)
                {
                    await LoadMembersAsync(CurrentOrganisation.Id);
                    await LoadStatsAsync();
                }
                else
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        _allMembers.Clear();
                        Members.Clear();
                        CurrentUserRole = null;
                        HousesCount = 0;
                        ActiveTenantsCount = 0;
                    });
                }
            }
            catch (Exception ex)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                    _dialogService.ShowMessage($"Could not load organisations: {ex.Message}", "Error"));
            }
            finally
            {
                await Dispatcher.UIThread.InvokeAsync(() => IsBusy = false);
            }
        }

        private async Task LoadMembersAsync(Guid orgId)
        {
            var list = await _memberService.GetMembersAsync(orgId);
            var currentEmail = _authSession.Email ?? string.Empty;
            var currentUserRole = list
                .Where(m => string.Equals(m.Email, currentEmail, StringComparison.OrdinalIgnoreCase))
                .Select(m => (OrgRole?)m.Role)
                .FirstOrDefault();

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    _allMembers = list.Select(m => new MemberVm(m, string.Equals(m.Email, currentEmail, StringComparison.OrdinalIgnoreCase))).ToList();
                    CurrentUserRole = currentUserRole;
                    ApplyMemberFilter();
                    OnPropertyChanged(nameof(IsMembersListEmpty));
                });
        }

        private async Task LoadStatsAsync()
        {
            try
            {
                var houses = await _houseService.GetAllHousesAsync(includeArchived: false);
                var houseList = houses.ToList();
                var housesCount = houseList.Count;
                var activeTenantsCount = houseList.Sum(h => h.ActiveTenantCount);
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    HousesCount = housesCount;
                    ActiveTenantsCount = activeTenantsCount;
                });
            }
            catch
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    HousesCount = 0;
                    ActiveTenantsCount = 0;
                });
            }
        }

        private async Task SwitchOrgAsync()
        {
            if (SelectedOrganisation == null || SelectedOrganisation.Id == CurrentOrganisation?.Id) return;
            if (IsBusy) return;
            IsBusy = true;
            try
            {
                await _orgContext.SetCurrentOrgAsync(SelectedOrganisation.Id);
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    CurrentOrganisation = SelectedOrganisation;
                    _ = LoadMembersAsync(SelectedOrganisation.Id);
                    _ = LoadStatsAsync();
                });
                ((RelayCommand)SwitchOrgCommand).RaiseCanExecuteChanged();
            }
            catch (Exception ex)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                    _dialogService.ShowMessage($"Could not switch organisation: {ex.Message}", "Error"));
            }
            finally
            {
                await Dispatcher.UIThread.InvokeAsync(() => IsBusy = false);
            }
        }

        private async Task CreateOrgAsync()
        {
            var name = await _dialogService.ShowInputDialogAsync("Enter organisation name:", "Create organisation", "My Organisation");
            if (string.IsNullOrWhiteSpace(name)) return;
            if (IsBusy) return;
            IsBusy = true;
            try
            {
                var org = await _orgService.CreateOrganisationAsync(name.Trim());
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    Organisations.Add(org);
                    SelectedOrganisation = org;
                    CurrentOrganisation = org;
                });
                await LoadMembersAsync(org.Id);
                await LoadStatsAsync();
                await Dispatcher.UIThread.InvokeAsync(() => _dialogService.ShowMessage($"Organisation '{org.Name}' created.", "Created"));
            }
            catch (Exception ex)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                    _dialogService.ShowMessage($"Could not create organisation: {ex.Message}", "Error"));
            }
            finally
            {
                await Dispatcher.UIThread.InvokeAsync(() => IsBusy = false);
            }
        }

        private async Task RenameOrgAsync()
        {
            if (CurrentOrganisation == null || !CanRenameOrg) return;
            var newName = await _dialogService.ShowInputDialogAsync("New organisation name:", "Rename organisation", CurrentOrganisation.Name);
            if (string.IsNullOrWhiteSpace(newName) || newName.Trim() == CurrentOrganisation.Name) return;
            if (IsBusy) return;
            IsBusy = true;
            try
            {
                await _orgService.RenameOrganisationAsync(CurrentOrganisation.Id, newName.Trim());
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    CurrentOrganisation = new Organisation
                    {
                        Id = CurrentOrganisation.Id,
                        Name = newName.Trim(),
                        CreatedAt = CurrentOrganisation.CreatedAt,
                        PlanTier = CurrentOrganisation.PlanTier
                    };
                    var idx = Organisations.ToList().FindIndex(o => o.Id == CurrentOrganisation.Id);
                    if (idx >= 0)
                    {
                        Organisations.RemoveAt(idx);
                        Organisations.Insert(idx, CurrentOrganisation);
                    }
                    if (SelectedOrganisation?.Id == CurrentOrganisation.Id)
                        SelectedOrganisation = CurrentOrganisation;
                    OnPropertyChanged(nameof(CurrentOrganisationName));
                    _dialogService.ShowMessage("Organisation renamed.", "Saved");
                });
            }
            catch (Exception ex)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                    _dialogService.ShowMessage($"Could not rename: {ex.Message}", "Error"));
            }
            finally
            {
                await Dispatcher.UIThread.InvokeAsync(() => IsBusy = false);
            }
        }

        private async Task InviteMemberAsync()
        {
            if (CurrentOrganisation == null || !CanInvite) return;
            var email = InviteEmail?.Trim();
            if (string.IsNullOrWhiteSpace(email))
            {
                _dialogService.ShowMessage("Please enter an email address.", "Validation");
                return;
            }
            if (IsBusy) return;
            IsBusy = true;
            try
            {
                var member = await _memberService.InviteMemberAsync(CurrentOrganisation.Id, email, InviteRole);
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    var currentEmail = _authSession.Email ?? string.Empty;
                    _allMembers.Add(new MemberVm(member, string.Equals(member.Email, currentEmail, StringComparison.OrdinalIgnoreCase)));
                    ApplyMemberFilter();
                    InviteEmail = string.Empty;
                    ((RelayCommand)InviteMemberCommand).RaiseCanExecuteChanged();
                    _dialogService.ShowMessage($"Invite sent to {email}.", "Invite sent");
                });
            }
            catch (Exception ex)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                    _dialogService.ShowMessage(ex.Message, "Error"));
            }
            finally
            {
                await Dispatcher.UIThread.InvokeAsync(() => IsBusy = false);
            }
        }

        private async Task ChangeRoleAsync(MemberVm? memberVm)
        {
            if (memberVm == null || !CanChangeRoleFor(memberVm)) return;
            var options = new[] { OrgRole.Admin, OrgRole.Member, OrgRole.ReadOnly };
            var current = memberVm.Role;
            // Simple approach: show message with role choices; user could pick from menu in UI. For now use a dialog.
            var choice = await _dialogService.ShowInputDialogAsync(
                "Enter new role (Admin, Member, ReadOnly):", "Change role", current.ToString());
            if (string.IsNullOrWhiteSpace(choice)) return;
            if (!Enum.TryParse<OrgRole>(choice.Trim(), true, out var newRole) || newRole == OrgRole.Owner || newRole == current)
            {
                _dialogService.ShowMessage("Invalid role. Use Admin, Member, or ReadOnly.", "Validation");
                return;
            }
            if (IsBusy) return;
            IsBusy = true;
            try
            {
                await _memberService.UpdateRoleAsync(memberVm.Id, newRole);
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    memberVm.Role = newRole;
                    _dialogService.ShowMessage($"Role updated to {newRole}.", "Updated");
                });
            }
            catch (Exception ex)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                    _dialogService.ShowMessage(ex.Message, "Error"));
            }
            finally
            {
                await Dispatcher.UIThread.InvokeAsync(() => IsBusy = false);
            }
        }

        private async Task RemoveMemberAsync(MemberVm? memberVm)
        {
            if (memberVm == null || !CanRemoveMember(memberVm)) return;
            var confirmed = await _dialogService.ShowConfirmationAsync(
                $"Remove {memberVm.DisplayName ?? memberVm.Email} from the team? They will lose access to this organisation.",
                "Remove member");
            if (!confirmed) return;
            if (IsBusy) return;
            IsBusy = true;
            try
            {
                await _memberService.RemoveMemberAsync(memberVm.Id);
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    _allMembers.Remove(memberVm);
                    ApplyMemberFilter();
                    OnPropertyChanged(nameof(IsMembersListEmpty));
                    _dialogService.ShowMessage("Member removed.", "Done");
                });
            }
            catch (Exception ex)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                    _dialogService.ShowMessage(ex.Message, "Error"));
            }
            finally
            {
                await Dispatcher.UIThread.InvokeAsync(() => IsBusy = false);
            }
        }
    }
}
