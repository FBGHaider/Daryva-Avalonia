namespace Daryva.Api.Dtos;

public class DiagnosticDataCountsResponse
{
    public string Message { get; set; } = string.Empty;
    public DiagnosticCounts Counts { get; set; } = new();
    public List<DiagnosticOrganizationSummary> Organizations { get; set; } = new();
    public List<DiagnosticGroupSummary> HouseSummary { get; set; } = new();
    public List<DiagnosticGroupSummary> TenantSummary { get; set; } = new();
}

public class DiagnosticCounts
{
    public int Organizations { get; set; }
    public int Houses { get; set; }
    public int Tenants { get; set; }
    public int Tenancies { get; set; }
    public int Expenses { get; set; }
    public int Documents { get; set; }
    public int RentPayments { get; set; }
    public int DepositPayments { get; set; }
}

public class DiagnosticOrganizationSummary
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class DiagnosticGroupSummary
{
    public Guid OrganizationId { get; set; }
    public int Count { get; set; }
    public List<string> Names { get; set; } = new();
}

public class DiagnosticOrgMembersResponse
{
    public string Message { get; set; } = string.Empty;
    public List<DiagnosticMember> Members { get; set; } = new();
}

public class DiagnosticMember
{
    public string UserId { get; set; } = string.Empty;
    public Guid OrganizationId { get; set; }
    public string? OrganizationName { get; set; }
    public string Role { get; set; } = string.Empty;
    public DateTime JoinedAt { get; set; }
}
