using Daryva.MVVM.Models;
using Daryva.Services.Api;
using Daryva.Services.Business;

namespace Daryva.Services.Data;

/// <summary>
/// API-only implementation of ITenancyRepository. Uses tenancy API and IApiEntityIdMapper for int/Guid mapping.
/// </summary>
public class TenancyApiRepositoryAdapter : ITenancyRepository
{
    private readonly ITenancyApiService _api;
    private readonly IApiEntityIdMapper _mapper;

    public TenancyApiRepositoryAdapter(ITenancyApiService api, IApiEntityIdMapper mapper)
    {
        _api = api ?? throw new ArgumentNullException(nameof(api));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    }

    public async Task<IEnumerable<Tenancy>> GetTenanciesByHouseIdAsync(int houseId)
    {
        var apiHouseId = _mapper.TryGetHouseApiId(houseId);
        if (!apiHouseId.HasValue) return Array.Empty<Tenancy>();
        var list = await _api.GetTenanciesAsync(houseId: apiHouseId.Value, cancellationToken: default);
        return list.Select(MapToTenancy);
    }

    public async Task<IEnumerable<Tenancy>> GetTenanciesByTenantIdAsync(int tenantId)
    {
        var apiTenantId = _mapper.TryGetTenantApiId(tenantId);
        if (!apiTenantId.HasValue) return Array.Empty<Tenancy>();
        var list = await _api.GetTenanciesAsync(tenantId: apiTenantId.Value, cancellationToken: default);
        return list.Select(MapToTenancy);
    }

    public async Task<Tenancy?> GetTenancyByIdAsync(int tenancyId)
    {
        var apiId = _mapper.TryGetTenancyApiId(tenancyId);
        if (!apiId.HasValue) return null;
        var dto = await _api.GetTenancyAsync(apiId.Value, default);
        return dto == null ? null : MapToTenancy(dto);
    }

    public async Task<IEnumerable<Tenancy>> GetActiveTenanciesAsync()
    {
        var list = await _api.GetTenanciesAsync(activeOnly: true, cancellationToken: default);
        return list.Select(MapToTenancy);
    }

    public async Task<IEnumerable<Tenancy>> GetTenanciesActiveInPeriodAsync(int year, int month)
    {
        var list = await _api.GetTenanciesActiveInPeriodAsync(year, month, default);
        return list.Select(MapToTenancy);
    }

    public async Task<Dictionary<int, (int Month, int Year)>> GetRentStartByTenancyIdsAsync(IEnumerable<int> tenancyIds)
    {
        var dict = new Dictionary<int, (int, int)>();
        foreach (var id in tenancyIds.Distinct())
        {
            var apiId = _mapper.TryGetTenancyApiId(id);
            if (!apiId.HasValue) continue;
            var t = await _api.GetTenancyAsync(apiId.Value, default);
            if (t?.RentStartMonth is >= 1 and <= 12 && t.RentStartYear is >= 2000 and <= 2100)
                dict[id] = (t.RentStartMonth.Value, t.RentStartYear!.Value);
        }
        return dict;
    }

    public async Task<int> CreateTenancyAsync(Tenancy tenancy)
    {
        var houseApiId = _mapper.TryGetHouseApiId(tenancy.HouseId);
        var tenantApiId = _mapper.TryGetTenantApiId(tenancy.TenantId);
        if (!houseApiId.HasValue || !tenantApiId.HasValue)
            throw new InvalidOperationException("House and Tenant must have API IDs to create tenancy via API.");
        var dto = new CreateTenancyDto
        {
            HouseId = houseApiId.Value,
            TenantId = tenantApiId.Value,
            MoveInDate = tenancy.MoveInDate,
            MoveOutDate = tenancy.MoveOutDate,
            RentStartMonth = tenancy.RentStartMonth,
            RentStartYear = tenancy.RentStartYear,
            RentAmountMonthly = tenancy.RentAmountMonthly,
            DepositAmount = tenancy.DepositAmount,
            PaymentDueDay = tenancy.PaymentDueDay,
            Status = tenancy.Status ?? "Active",
            Notes = tenancy.Notes
        };
        var guid = await _api.CreateTenancyAsync(dto, default);
        return _mapper.MapTenancyId(guid);
    }

    public async Task UpdateTenancyAsync(Tenancy tenancy)
    {
        var apiId = _mapper.TryGetTenancyApiId(tenancy.TenancyId);
        if (!apiId.HasValue)
            throw new InvalidOperationException("Tenancy must have API ID to update via API.");
        var dto = new UpdateTenancyDto
        {
            MoveInDate = tenancy.MoveInDate,
            MoveOutDate = tenancy.MoveOutDate,
            RentStartMonth = tenancy.RentStartMonth,
            RentStartYear = tenancy.RentStartYear,
            RentAmountMonthly = tenancy.RentAmountMonthly,
            DepositAmount = tenancy.DepositAmount,
            PaymentDueDay = tenancy.PaymentDueDay,
            Status = tenancy.Status,
            Notes = tenancy.Notes
        };
        await _api.UpdateTenancyAsync(apiId.Value, dto, default);
    }

    public async Task EndTenancyAsync(int tenancyId, DateTime moveOutDate)
    {
        var apiId = _mapper.TryGetTenancyApiId(tenancyId);
        if (!apiId.HasValue)
            throw new InvalidOperationException("Tenancy not found or not from API.");
        await _api.EndTenancyAsync(apiId.Value, moveOutDate, default);
    }

    public async Task ReactivateTenancyAsync(int tenancyId)
    {
        var apiId = _mapper.TryGetTenancyApiId(tenancyId);
        if (!apiId.HasValue)
            throw new InvalidOperationException("Tenancy not found or not from API.");
        await _api.ReactivateTenancyAsync(apiId.Value, default);
    }

    public async Task<IEnumerable<Tenancy>> GetEndedTenanciesWithDepositAsync()
    {
        var list = await _api.GetEndedTenanciesWithDepositAsync(default);
        return list.Select(MapToTenancy);
    }

    public async Task DeleteTenancyAsync(int tenancyId)
    {
        var apiId = _mapper.TryGetTenancyApiId(tenancyId);
        if (!apiId.HasValue)
            throw new InvalidOperationException("Tenancy not found or not from API.");
        await _api.DeleteTenancyAsync(apiId.Value, default);
    }

    public async Task DeleteEndedTenanciesByHouseIdAsync(int houseId)
    {
        var apiHouseId = _mapper.TryGetHouseApiId(houseId);
        if (!apiHouseId.HasValue) return;
        await _api.DeleteEndedTenanciesByHouseIdAsync(apiHouseId.Value, default);
    }

    private Tenancy MapToTenancy(TenancyDetailDto dto)
    {
        var tenancy = new Tenancy
        {
            TenancyId = _mapper.MapTenancyId(dto.Id),
            HouseId = _mapper.MapHouseId(dto.HouseId),
            TenantId = _mapper.MapTenantId(dto.TenantId),
            MoveInDate = dto.MoveInDate,
            MoveOutDate = dto.MoveOutDate,
            RentStartMonth = dto.RentStartMonth,
            RentStartYear = dto.RentStartYear,
            RentAmountMonthly = dto.RentAmountMonthly,
            DepositAmount = dto.DepositAmount,
            PaymentDueDay = dto.PaymentDueDay,
            Status = dto.Status ?? "Active",
            Notes = dto.Notes
        };
        if (dto.House != null)
            tenancy.House = new House
            {
                HouseId = _mapper.MapHouseId(dto.House.Id),
                ApiId = dto.House.Id,
                AddressLine1 = dto.House.AddressLine1,
                AddressLine2 = dto.House.AddressLine2,
                City = dto.House.City,
                Postcode = dto.House.Postcode,
                TotalRooms = dto.House.TotalRooms,
                CreatedAt = dto.House.CreatedAt
            };
        if (dto.Tenant != null)
            tenancy.Tenant = new Tenant
            {
                TenantId = _mapper.MapTenantId(dto.Tenant.Id),
                ApiId = dto.Tenant.Id,
                FullName = dto.Tenant.FullName,
                PhoneNumber = dto.Tenant.PhoneNumber ?? string.Empty,
                Email = dto.Tenant.Email ?? string.Empty,
                UniversityName = dto.Tenant.UniversityName,
                CreatedAt = dto.Tenant.CreatedAt,
                IsArchived = dto.Tenant.IsArchived
            };
        return tenancy;
    }
}
