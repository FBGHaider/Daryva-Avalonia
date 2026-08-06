using Daryva.Api.Domain;
using Daryva.Api.Dtos;
using Daryva.Api.Repositories.Interfaces;
using Daryva.Api.Security.Interfaces;
using Daryva.Api.Services.Interfaces;

namespace Daryva.Api.Services;

/// <summary>
/// Implementation of IHouseService.
/// All queries are automatically filtered by OrgId via EF Core global query filters.
/// </summary>
public class HouseService : IHouseService
{
    private readonly IHouseRepository _houseRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<HouseService> _logger;
    private readonly IRentLedgerService _rentLedgerService;
    private readonly ITenantContext _tenantContext;
    private readonly IAuditLogger _auditLogger;

    public HouseService(
        IHouseRepository houseRepository,
        IUnitOfWork unitOfWork,
        ILogger<HouseService> logger,
        IRentLedgerService rentLedgerService,
        ITenantContext tenantContext,
        IAuditLogger auditLogger)
    {
        _houseRepository = houseRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _rentLedgerService = rentLedgerService;
        _tenantContext = tenantContext;
        _auditLogger = auditLogger;
    }

    public async Task<IEnumerable<HouseResponse>> GetHousesAsync(
        Guid orgId,
        bool includeArchived = false,
        CancellationToken cancellationToken = default)
    {
        var houses = await _houseRepository.GetAllAsync(includeArchived, cancellationToken);
        var activeTenancyStats = await BuildCurrentActiveTenancyStatsAsync(cancellationToken);

        return houses.Select(house =>
        {
            if (activeTenancyStats.TryGetValue(house.Id, out var stats))
            {
                return MapToResponse(house, stats.ActiveTenantCount, stats.TotalMonthlyRent);
            }

            return MapToResponse(house, 0, 0m);
        }).ToList();
    }

    public async Task<HouseResponse?> GetHouseAsync(
        Guid orgId,
        Guid houseId,
        CancellationToken cancellationToken = default)
    {
        var house = await _houseRepository.GetByIdAsync(houseId, cancellationToken);
        if (house == null)
            return null;

        var statsByHouse = await BuildCurrentActiveTenancyStatsAsync(cancellationToken);
        if (statsByHouse.TryGetValue(houseId, out var stats))
        {
            return MapToResponse(house, stats.ActiveTenantCount, stats.TotalMonthlyRent);
        }

        return MapToResponse(house, 0, 0m);
    }

    public async Task<HouseResponse> CreateHouseAsync(
        Guid orgId,
        CreateHouseRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateHouseRequest(request);

        var house = new House
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId, // ✅ Server-side assignment; client cannot override
            Name = request.Name.Trim(),
            AddressLine1 = request.AddressLine1.Trim(),
            AddressLine2 = request.AddressLine2?.Trim(),
            City = request.City.Trim(),
            Postcode = request.Postcode.Trim(),
            TotalRooms = request.TotalRooms,
            CreatedAt = DateTime.UtcNow
        };

        _houseRepository.Add(house);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created house {HouseId} in organization {OrgId}.", house.Id, orgId);

        return MapToResponse(house, 0, 0m);
    }

    public async Task<HouseResponse?> UpdateHouseAsync(
        Guid orgId,
        Guid houseId,
        UpdateHouseRequest request,
        CancellationToken cancellationToken = default)
    {
        var house = await _houseRepository.GetTrackedByIdAsync(houseId, cancellationToken);

        if (house == null)
            return null;

        // Update only non-null fields
        if (!string.IsNullOrWhiteSpace(request.Name))
            house.Name = request.Name.Trim();

        if (!string.IsNullOrWhiteSpace(request.AddressLine1))
            house.AddressLine1 = request.AddressLine1.Trim();

        if (request.AddressLine2 != null)
            house.AddressLine2 = request.AddressLine2.Trim();

        if (!string.IsNullOrWhiteSpace(request.City))
            house.City = request.City.Trim();

        if (!string.IsNullOrWhiteSpace(request.Postcode))
            house.Postcode = request.Postcode.Trim();

        if (request.TotalRooms.HasValue && request.TotalRooms.Value < 0)
            throw new ArgumentException("Total rooms cannot be negative.", nameof(request.TotalRooms));

        if (request.TotalRooms.HasValue)
            house.TotalRooms = request.TotalRooms.Value;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Updated house {HouseId} in organization {OrgId}.", houseId, orgId);

        return MapToResponse(house, 0, 0m);
    }

    public async Task<HouseResponse?> ArchiveHouseAsync(
        Guid orgId,
        Guid houseId,
        CancellationToken cancellationToken = default)
    {
        var house = await _houseRepository.GetTrackedByIdAsync(houseId, cancellationToken);

        if (house == null)
            return null;

        house.IsArchived = true;
        LogAudit(AuditEventTypes.HouseArchived, house.OrganizationId, nameof(House), house.Id.ToString());
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Archived house {HouseId} in organization {OrgId}.", houseId, orgId);

        var statsByHouse = await BuildCurrentActiveTenancyStatsAsync(cancellationToken);
        var (activeCount, totalRent) = statsByHouse.TryGetValue(houseId, out var s) ? s : (0, 0m);
        return MapToResponse(house, activeCount, totalRent);
    }

    public async Task<bool> DeleteHouseAsync(
        Guid orgId,
        Guid houseId,
        CancellationToken cancellationToken = default)
    {
        var house = await _houseRepository.GetTrackedByIdAsync(houseId, cancellationToken);

        if (house == null)
            return false;

        _houseRepository.Remove(house);
        LogAudit(AuditEventTypes.HouseDeleted, house.OrganizationId, nameof(House), house.Id.ToString());
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Deleted house {HouseId} from organization {OrgId}.", houseId, orgId);

        return true;
    }

    private void LogAudit(string eventType, Guid organizationId, string targetType, string targetId)
    {
        if (!Guid.TryParse(_tenantContext.UserId, out var actorId))
            return;

        _auditLogger.Log(actorId, _tenantContext.CurrentRole ?? "Unknown", eventType,
            organizationId: organizationId, targetType: targetType, targetId: targetId,
            supportSessionId: _tenantContext.ActiveSupportSessionId);
    }

    private static void ValidateHouseRequest(CreateHouseRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("House name cannot be empty.", nameof(request.Name));

        if (string.IsNullOrWhiteSpace(request.AddressLine1))
            throw new ArgumentException("Address line 1 cannot be empty.", nameof(request.AddressLine1));

        if (string.IsNullOrWhiteSpace(request.City))
            throw new ArgumentException("City cannot be empty.", nameof(request.City));

        if (string.IsNullOrWhiteSpace(request.Postcode))
            throw new ArgumentException("Postcode cannot be empty.", nameof(request.Postcode));

        if (request.TotalRooms < 0)
            throw new ArgumentException("Total rooms cannot be negative.", nameof(request.TotalRooms));
    }

    private static HouseResponse MapToResponse(House house, int activeTenantCount, decimal totalMonthlyRent)
        => new()
        {
            Id = house.Id,
            OrganizationId = house.OrganizationId,
            Name = house.Name,
            AddressLine1 = house.AddressLine1,
            AddressLine2 = house.AddressLine2,
            City = house.City,
            Postcode = house.Postcode,
            TotalRooms = house.TotalRooms,
            CreatedAt = house.CreatedAt,
            ActiveTenantCount = activeTenantCount,
            TotalMonthlyRent = totalMonthlyRent,
            IsArchived = house.IsArchived
        };

    /// <summary>
    /// Builds house stats from the same data as the rent ledger for the current month,
    /// so house "Monthly Rent" and "Active Tenants" always match the Rent & Payments tab.
    /// </summary>
    private async Task<Dictionary<Guid, (int ActiveTenantCount, decimal TotalMonthlyRent)>> BuildCurrentActiveTenancyStatsAsync(
        CancellationToken cancellationToken)
    {
        var today = DateTime.UtcNow.Date;
        var ledgerEntries = await _rentLedgerService.GetRentLedgerEntriesAsync(
            today.Year,
            today.Month,
            houseId: null,
            statusFilter: null,
            searchTerm: null,
            cancellationToken);

        var byHouse = ledgerEntries
            .GroupBy(e => e.HouseId)
            .ToDictionary(
                g => g.Key,
                g => (
                    ActiveTenantCount: g.Count(),
                    TotalMonthlyRent: g.Sum(e => e.AmountDue)));

        foreach (var kv in byHouse)
            _logger.LogInformation("[DIAGNOSTIC] HouseId={HouseId} ActiveTenants={Count} TotalMonthlyRent={Total:F2} (from rent ledger)", kv.Key, kv.Value.ActiveTenantCount, kv.Value.TotalMonthlyRent);

        return byHouse;
    }
}
