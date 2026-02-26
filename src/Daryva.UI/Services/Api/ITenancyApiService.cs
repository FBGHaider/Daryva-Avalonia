namespace Daryva.Services.Api;

/// <summary>
/// DTO for creating a tenancy via the API.
/// </summary>
public class CreateTenancyDto
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

public class TenancyDetailDto
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

public class UpdateTenancyDto
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

/// <summary>
/// Service for tenancy-related API operations.
/// </summary>
public interface ITenancyApiService
{
    Task<Guid> CreateTenancyAsync(CreateTenancyDto dto, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TenancyDetailDto>> GetTenanciesAsync(Guid? tenantId = null, Guid? houseId = null, bool? activeOnly = null, CancellationToken cancellationToken = default);
    Task<TenancyDetailDto?> GetTenancyAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TenancyDetailDto>> GetTenanciesActiveInPeriodAsync(int year, int month, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TenancyDetailDto>> GetEndedTenanciesWithDepositAsync(CancellationToken cancellationToken = default);
    Task EndTenancyAsync(Guid id, DateTime moveOutDate, CancellationToken cancellationToken = default);
    Task ReactivateTenancyAsync(Guid id, CancellationToken cancellationToken = default);
    Task UpdateTenancyAsync(Guid id, UpdateTenancyDto dto, CancellationToken cancellationToken = default);
    Task DeleteTenancyAsync(Guid id, CancellationToken cancellationToken = default);
    Task DeleteEndedTenanciesByHouseIdAsync(Guid houseId, CancellationToken cancellationToken = default);
}
