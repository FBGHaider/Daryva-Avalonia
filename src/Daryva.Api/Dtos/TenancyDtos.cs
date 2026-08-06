namespace Daryva.Api.Dtos;

public class RentRepairExportItem
{
    public Guid TenancyId { get; set; }
    public string TenantName { get; set; } = string.Empty;
    public string HouseName { get; set; } = string.Empty;
    public decimal RentAmountMonthly { get; set; }
    public decimal DepositAmount { get; set; }
}

public class RentRepairRequest
{
    public List<RentRepairUpdateItem> Updates { get; set; } = new();
}

public class RentRepairUpdateItem
{
    public Guid TenancyId { get; set; }
    public decimal RentAmountMonthly { get; set; }
    public decimal? DepositAmount { get; set; }
}

public class RentRepairResult
{
    public int UpdatedCount { get; set; }
    public List<string> Errors { get; set; } = new();
}

public class TenancyLookupResponse
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid HouseId { get; set; }
    public DateTime? MoveOutDate { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class CreateTenancyRequest
{
    public Guid HouseId { get; set; }
    public Guid TenantId { get; set; }
    public DateTime MoveInDate { get; set; }
    public DateTime? MoveOutDate { get; set; }
    public int? RentStartMonth { get; set; }
    public int? RentStartYear { get; set; }
    public decimal RentAmountMonthly { get; set; }
    public decimal DepositAmount { get; set; }
    public byte PaymentDueDay { get; set; }
    public string? Status { get; set; }
    public string? Notes { get; set; }
}

public class CreateTenancyResponse
{
    public Guid Id { get; set; }
}

public class TenancyDetailResponse
{
    public Guid Id { get; set; }
    public Guid HouseId { get; set; }
    public Guid TenantId { get; set; }
    public DateTime MoveInDate { get; set; }
    public DateTime? MoveOutDate { get; set; }
    public int? RentStartMonth { get; set; }
    public int? RentStartYear { get; set; }
    public decimal RentAmountMonthly { get; set; }
    public decimal DepositAmount { get; set; }
    public byte PaymentDueDay { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public TenancyHouseDto? House { get; set; }
    public TenancyTenantDto? Tenant { get; set; }
}

public class TenancyHouseDto
{
    public Guid Id { get; set; }
    public string AddressLine1 { get; set; } = string.Empty;
    public string? AddressLine2 { get; set; }
    public string City { get; set; } = string.Empty;
    public string Postcode { get; set; } = string.Empty;
    public int TotalRooms { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class TenancyTenantDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? UniversityName { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsArchived { get; set; }
}

public class EndTenancyRequest
{
    public DateTime MoveOutDate { get; set; }
}

public class UpdateTenancyRequest
{
    public DateTime MoveInDate { get; set; }
    public DateTime? MoveOutDate { get; set; }
    public int? RentStartMonth { get; set; }
    public int? RentStartYear { get; set; }
    public decimal RentAmountMonthly { get; set; }
    public decimal DepositAmount { get; set; }
    public byte PaymentDueDay { get; set; }
    public string? Status { get; set; }
    public string? Notes { get; set; }
}
