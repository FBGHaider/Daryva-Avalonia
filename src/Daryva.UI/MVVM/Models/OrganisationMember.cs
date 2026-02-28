namespace Daryva.MVVM.Models
{
    /// <summary>
    /// Role within an organisation.
    /// </summary>
    public enum OrgRole
    {
        Owner,
        Admin,
        Member,
        ReadOnly
    }

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
    /// </summary>
    public class OrganisationMember
    {
        public Guid Id { get; set; }
        public Guid OrganisationId { get; set; }
        public string? DisplayName { get; set; }
        public string Email { get; set; } = string.Empty;
        public OrgRole Role { get; set; }
        public MemberStatus Status { get; set; }
        public DateTime JoinedAt { get; set; }
    }
}
