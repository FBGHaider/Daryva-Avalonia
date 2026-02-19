namespace Daryva.Api.Domain;

/// <summary>
/// One-time invite token for a user to join an organization.
/// </summary>
public class OrganizationInvite
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string Role { get; set; } = OrganizationMember.Roles.Member;
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public string? UsedByUserId { get; set; }
    public DateTime? RevokedAt { get; set; }

    public Organization? Organization { get; set; }
}

/// <summary>
/// Reusable organization join code.
/// </summary>
public class OrganizationJoinCode
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public string CodeHash { get; set; } = string.Empty;
    public string Role { get; set; } = OrganizationMember.Roles.Member;
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }

    public Organization? Organization { get; set; }
}
