using System.Windows.Input;
using Daryva.MVVM.Commands;
using Daryva.Services.Business;
using Daryva.Services.Dialog;
using Daryva.Services.OrgContext;

namespace Daryva.MVVM.ViewModels
{
    /// <summary>Backs InviteMemberDialog. Email-only -- IOrganisationMemberService.InviteMemberAsync
    /// has no role parameter, and this codebase's own OrganisationMember model documents removing a
    /// prior fake role concept that "didn't correspond to anything real." There is genuinely only
    /// one role today (Landlord), so a role picker would recreate that exact bug.</summary>
    public class InviteMemberViewModel : BaseViewModel
    {
        private readonly IOrganisationMemberService _memberService;
        private readonly IOrgContext _orgContext;
        private readonly IDialogService _dialogService;

        private string _email = string.Empty;
        private bool _isBusy;

        public InviteMemberViewModel(IOrganisationMemberService memberService, IOrgContext orgContext, IDialogService dialogService)
        {
            _memberService = memberService;
            _orgContext = orgContext;
            _dialogService = dialogService;
            SendInviteCommand = new RelayCommand(async _ => await SendInviteAsync(), _ => CanSendInvite());
        }

        public event EventHandler? CloseRequested;

        public ICommand SendInviteCommand { get; }

        public string Email
        {
            get => _email;
            set
            {
                SetProperty(ref _email, value ?? string.Empty);
                ((RelayCommand)SendInviteCommand).RaiseCanExecuteChanged();
            }
        }

        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                SetProperty(ref _isBusy, value);
                ((RelayCommand)SendInviteCommand).RaiseCanExecuteChanged();
            }
        }

        private bool CanSendInvite()
        {
            return !IsBusy && !string.IsNullOrWhiteSpace(Email) && _orgContext.CurrentOrgId.HasValue;
        }

        private async Task SendInviteAsync()
        {
            if (!_orgContext.CurrentOrgId.HasValue)
                return;

            IsBusy = true;
            try
            {
                await _memberService.InviteMemberAsync(_orgContext.CurrentOrgId.Value, Email.Trim());
                _dialogService.ShowMessage($"Invite sent to {Email.Trim()}.", "Invite sent");
                CloseRequested?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error sending invite: {ex.Message}", "Error");
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
