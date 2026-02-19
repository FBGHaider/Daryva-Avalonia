namespace Daryva.Api.Dtos;

public class CreateOrgInviteRequest
{
    public string? Email { get; set; }
    public string Role { get; set; } = "Member";
    public int ExpiresInDays { get; set; } = 7;
}

public class CreateOrgInviteResponse
{
    public Guid InviteId { get; set; }
    public Guid OrganizationId { get; set; }
    public string Token { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string Role { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}

public class AcceptOrgInviteRequest
{
    public string Token { get; set; } = string.Empty;
}

public class GenerateOrgJoinCodeRequest
{
    public string Role { get; set; } = "Member";
    public int? ExpiresInDays { get; set; } = 30;
}

public class GenerateOrgJoinCodeResponse
{
    public Guid JoinCodeId { get; set; }
    public Guid OrganizationId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public DateTime? ExpiresAt { get; set; }
}

public class JoinOrgByCodeRequest
{
    public string Code { get; set; } = string.Empty;
}

public class JoinOrganizationResponse
{
    public Guid OrganizationId { get; set; }
    public string OrganizationName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool AlreadyMember { get; set; }
}
