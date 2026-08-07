namespace Daryva.Api.Dtos;

public class CreateTenantRequest
{
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? UniversityName { get; set; }
}

public class UpdateTenantRequest
{
    public string? FullName { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? UniversityName { get; set; }
}

public class TenantResponse
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? UniversityName { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsArchived { get; set; }

    // House/Tenancy information
    public string? CurrentHouseAddress { get; set; }
    public Guid? CurrentHouseId { get; set; }
    public Guid? CurrentTenancyId { get; set; }
    public DateTime? LeaveDate { get; set; }

    // Tenant portal status -- AppUserId set means they've completed signup;
    // InviteSentAt set with no AppUserId means an invite is outstanding.
    public Guid? AppUserId { get; set; }
    public DateTime? InviteSentAt { get; set; }
    public DateTime? InviteAcceptedAt { get; set; }
}
