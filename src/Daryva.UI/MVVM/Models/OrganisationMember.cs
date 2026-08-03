namespace Daryva.MVVM.Models
{
    /// <summary>
    /// Member status (Active = accepted, Pending = invited).
    /// </summary>
    public enum MemberStatus
    {
        Active,
        Pending
    }

    /// <summary>
    /// Organisation member (team member or pending invite).
    ///
    /// Role matches the backend's actual model (Domain/OrganizationMember.cs): a single string
    /// role, currently only ever "Landlord", plus IsPrimaryOwner distinguishing the org's one
    /// primary owner (exclusive rights: rename org, delete org) from co-managing Landlords who
    /// otherwise hold the same permissions. There is no Admin/Member/ReadOnly tier on the server --
    /// a prior desktop-only OrgRole enum with those values didn't correspond to anything real and
    /// silently mapped every actual member to the wrong role.
    /// </summary>
    public class OrganisationMember
    {
        public const string LandlordRole = "Landlord";

        public Guid Id { get; set; }
        public Guid OrganisationId { get; set; }
        public string? DisplayName { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = LandlordRole;
        public bool IsPrimaryOwner { get; set; }
        public MemberStatus Status { get; set; }
        public DateTime JoinedAt { get; set; }
    }
}
