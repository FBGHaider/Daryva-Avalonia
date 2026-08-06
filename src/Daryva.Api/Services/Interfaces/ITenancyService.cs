using Daryva.Api.Dtos;

namespace Daryva.Api.Services.Interfaces;

public interface ITenancyService
{
    Task<List<TenancyDetailResponse>> GetTenanciesAsync(Guid? tenantId, Guid? houseId, bool? activeOnly, CancellationToken cancellationToken = default);

    Task<List<TenancyDetailResponse>> GetTenanciesActiveInPeriodAsync(int year, int month, CancellationToken cancellationToken = default);

    Task<TenancyDetailResponse?> GetTenancyAsync(Guid id, CancellationToken cancellationToken = default);

    Task<List<TenancyDetailResponse>> GetEndedTenanciesWithDepositAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns false if the tenancy was not found.</summary>
    Task<bool> EndTenancyAsync(Guid id, DateTime moveOutDate, CancellationToken cancellationToken = default);

    /// <summary>Returns false if the tenancy was not found.</summary>
    Task<bool> ReactivateTenancyAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Returns false if the tenancy was not found.</summary>
    Task<bool> UpdateTenancyAsync(Guid id, UpdateTenancyRequest request, CancellationToken cancellationToken = default);

    /// <summary>Returns false if the tenancy was not found.</summary>
    Task<bool> DeleteTenancyAsync(Guid id, CancellationToken cancellationToken = default);

    Task DeleteEndedTenanciesByHouseAsync(Guid houseId, bool endedOnly, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates the request and creates the tenancy.
    /// Throws ArgumentException for validation failures, KeyNotFoundException if the house or tenant doesn't exist.
    /// </summary>
    Task<Guid> CreateTenancyAsync(CreateTenancyRequest request, CancellationToken cancellationToken = default);

    Task<List<RentRepairExportItem>> ExportForRentRepairAsync(CancellationToken cancellationToken = default);

    Task<RentRepairResult> RepairRentAsync(RentRepairRequest request, CancellationToken cancellationToken = default);
}
