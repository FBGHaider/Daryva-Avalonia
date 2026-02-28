using System;
using Daryva.MVVM.Models;

namespace Daryva.MVVM.ViewModels
{
    /// <summary>
    /// View model for a single organisation member (for binding in Organisation/Team page).
    /// </summary>
    public class MemberVm : BaseViewModel
    {
        private OrgRole _role;
        private MemberStatus _status;

        public MemberVm(OrganisationMember member, bool isCurrentUser)
        {
            Member = member ?? throw new ArgumentNullException(nameof(member));
            Id = member.Id;
            OrganisationId = member.OrganisationId;
            DisplayName = member.DisplayName;
            Email = member.Email;
            _role = member.Role;
            _status = member.Status;
            JoinedAt = member.JoinedAt;
            IsCurrentUser = isCurrentUser;
        }

        public OrganisationMember Member { get; }

        public Guid Id { get; }
        public Guid OrganisationId { get; }
        public string? DisplayName { get; }
        public string Email { get; }

        public OrgRole Role
        {
            get => _role;
            set
            {
                if (SetProperty(ref _role, value))
                {
                    Member.Role = value;
                    OnPropertyChanged(nameof(RoleDisplay));
                }
            }
        }

        public MemberStatus Status
        {
            get => _status;
            set
            {
                if (SetProperty(ref _status, value))
                {
                    Member.Status = value;
                    OnPropertyChanged(nameof(StatusDisplay));
                }
            }
        }

        public DateTime JoinedAt { get; }
        public bool IsCurrentUser { get; }

        public string RoleDisplay => Role.ToString();
        public string StatusDisplay => Status.ToString();

        /// <summary>Display name or email, with " (You)" suffix when current user.</summary>
        public string DisplayLabel => string.IsNullOrWhiteSpace(DisplayName) ? Email + (IsCurrentUser ? " (You)" : "") : DisplayName + (IsCurrentUser ? " (You)" : "");
    }
}
