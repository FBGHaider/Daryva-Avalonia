using Daryva.Api.Domain;
using Daryva.Api.Dtos;
using Daryva.Api.Repositories.Interfaces;
using Daryva.Api.Security.Interfaces;
using Daryva.Api.Services.Interfaces;

namespace Daryva.Api.Services;

public class TenancyService : ITenancyService
{
    private readonly ITenancyRepository _tenancyRepository;
    private readonly IHouseRepository _houseRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITenantContext _tenantContext;
    private readonly IAuditLogger _auditLogger;

    public TenancyService(
        ITenancyRepository tenancyRepository,
        IHouseRepository houseRepository,
        ITenantRepository tenantRepository,
        IUnitOfWork unitOfWork,
        ITenantContext tenantContext,
        IAuditLogger auditLogger)
    {
        _tenancyRepository = tenancyRepository;
        _houseRepository = houseRepository;
        _tenantRepository = tenantRepository;
        _unitOfWork = unitOfWork;
        _tenantContext = tenantContext;
        _auditLogger = auditLogger;
    }

    public async Task<List<TenancyDetailResponse>> GetTenanciesAsync(Guid? tenantId, Guid? houseId, bool? activeOnly, CancellationToken cancellationToken = default)
    {
        var tenancies = await _tenancyRepository.GetAllWithDetailsAsync(tenantId, houseId, activeOnly, cancellationToken);
        return tenancies.Select(MapToDetailResponse).ToList();
    }

    public async Task<List<TenancyDetailResponse>> GetTenanciesActiveInPeriodAsync(int year, int month, CancellationToken cancellationToken = default)
    {
        var periodStart = DateTime.SpecifyKind(new DateTime(year, month, 1), DateTimeKind.Utc);
        var periodEnd = periodStart.AddMonths(1);
        var tenancies = await _tenancyRepository.GetActiveInPeriodWithDetailsAsync(periodStart, periodEnd, cancellationToken);
        return tenancies.Select(MapToDetailResponse).ToList();
    }

    public async Task<TenancyDetailResponse?> GetTenancyAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenancy = await _tenancyRepository.GetByIdWithDetailsAsync(id, cancellationToken);
        return tenancy == null ? null : MapToDetailResponse(tenancy);
    }

    public async Task<List<TenancyDetailResponse>> GetEndedTenanciesWithDepositAsync(CancellationToken cancellationToken = default)
    {
        var tenancies = await _tenancyRepository.GetEndedWithDepositAsync(cancellationToken);
        return tenancies.Select(MapToDetailResponse).ToList();
    }

    public async Task<bool> EndTenancyAsync(Guid id, DateTime moveOutDate, CancellationToken cancellationToken = default)
    {
        var tenancy = await _tenancyRepository.GetTrackedByIdAsync(id, cancellationToken);
        if (tenancy == null)
            return false;

        var moveOutUtc = moveOutDate.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(moveOutDate, DateTimeKind.Utc)
            : moveOutDate.ToUniversalTime();
        tenancy.MoveOutDate = moveOutUtc;
        tenancy.Status = "Ended";
        LogAudit(AuditEventTypes.TenancyEnded, tenancy.OrganizationId, nameof(Tenancy), tenancy.Id.ToString());
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> ReactivateTenancyAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenancy = await _tenancyRepository.GetTrackedByIdAsync(id, cancellationToken);
        if (tenancy == null)
            return false;

        tenancy.MoveOutDate = null;
        tenancy.Status = "Active";
        LogAudit(AuditEventTypes.TenancyReactivated, tenancy.OrganizationId, nameof(Tenancy), tenancy.Id.ToString());
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> UpdateTenancyAsync(Guid id, UpdateTenancyRequest request, CancellationToken cancellationToken = default)
    {
        var tenancy = await _tenancyRepository.GetTrackedByIdAsync(id, cancellationToken);
        if (tenancy == null)
            return false;

        var moveIn = request.MoveInDate.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(request.MoveInDate.Date, DateTimeKind.Utc)
            : request.MoveInDate.ToUniversalTime();
        var moveOut = request.MoveOutDate.HasValue
            ? (DateTime?) (request.MoveOutDate.Value.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(request.MoveOutDate.Value.Date, DateTimeKind.Utc)
                : request.MoveOutDate.Value.ToUniversalTime())
            : null;

        tenancy.MoveInDate = moveIn;
        tenancy.MoveOutDate = moveOut;
        tenancy.RentStartMonth = request.RentStartMonth;
        tenancy.RentStartYear = request.RentStartYear;
        tenancy.RentAmountMonthly = request.RentAmountMonthly;
        tenancy.DepositAmount = request.DepositAmount;
        tenancy.PaymentDueDay = request.PaymentDueDay;
        tenancy.Status = string.IsNullOrWhiteSpace(request.Status) ? (tenancy.Status ?? "Active") : request.Status.Trim();
        tenancy.Notes = request.Notes;

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteTenancyAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenancy = await _tenancyRepository.GetTrackedByIdAsync(id, cancellationToken);
        if (tenancy == null)
            return false;

        _tenancyRepository.Remove(tenancy);
        LogAudit(AuditEventTypes.TenancyDeleted, tenancy.OrganizationId, nameof(Tenancy), tenancy.Id.ToString());
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task DeleteEndedTenanciesByHouseAsync(Guid houseId, bool endedOnly, CancellationToken cancellationToken = default)
    {
        var orgId = _tenantContext.CurrentOrgId!.Value;
        var toRemove = await _tenancyRepository.GetTrackedByHouseIdAsync(houseId, endedOnly, cancellationToken);
        _tenancyRepository.RemoveRange(toRemove);
        if (toRemove.Count > 0)
        {
            LogAudit(AuditEventTypes.TenanciesBulkDeleted, orgId, nameof(House), houseId.ToString());
        }
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<Guid> CreateTenancyAsync(CreateTenancyRequest request, CancellationToken cancellationToken = default)
    {
        if (request.RentAmountMonthly <= 0)
            throw new ArgumentException("Rent amount must be greater than 0.", nameof(request.RentAmountMonthly));
        if (request.PaymentDueDay < 1 || request.PaymentDueDay > 31)
            throw new ArgumentException("Payment due day must be between 1 and 31.", nameof(request.PaymentDueDay));

        var moveInDate = request.MoveInDate.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(request.MoveInDate.Date, DateTimeKind.Utc)
            : request.MoveInDate.ToUniversalTime();
        if (moveInDate.Year < 2000 || moveInDate.Year > 2100)
            throw new ArgumentException("Move-in date must be between 2000 and 2100.", nameof(request.MoveInDate));

        var orgId = _tenantContext.CurrentOrgId!.Value;

        var house = await _houseRepository.GetTrackedByIdAsync(request.HouseId, cancellationToken);
        if (house == null)
            throw new KeyNotFoundException("House not found.");

        var tenant = await _tenantRepository.GetTrackedByIdAsync(request.TenantId, cancellationToken);
        if (tenant == null)
            throw new KeyNotFoundException("Tenant not found.");

        var tenancy = new Tenancy
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            HouseId = request.HouseId,
            TenantId = request.TenantId,
            MoveInDate = moveInDate,
            MoveOutDate = request.MoveOutDate,
            RentStartMonth = request.RentStartMonth,
            RentStartYear = request.RentStartYear,
            RentAmountMonthly = request.RentAmountMonthly,
            DepositAmount = request.DepositAmount,
            PaymentDueDay = request.PaymentDueDay,
            Status = request.Status ?? "Active",
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim()
        };

        _tenancyRepository.Add(tenancy);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return tenancy.Id;
    }

    public async Task<List<RentRepairExportItem>> ExportForRentRepairAsync(CancellationToken cancellationToken = default)
    {
        var tenancies = await _tenancyRepository.GetAllWithDetailsForExportAsync(cancellationToken);
        return tenancies.Select(t => new RentRepairExportItem
        {
            TenancyId = t.Id,
            TenantName = t.Tenant!.FullName,
            HouseName = t.House!.Name,
            RentAmountMonthly = t.RentAmountMonthly,
            DepositAmount = t.DepositAmount
        }).ToList();
    }

    public async Task<RentRepairResult> RepairRentAsync(RentRepairRequest request, CancellationToken cancellationToken = default)
    {
        var tenancyIds = request.Updates.Select(u => u.TenancyId).Distinct().ToList();
        var tenancies = (await _tenancyRepository.GetTrackedByIdsAsync(tenancyIds, cancellationToken))
            .ToDictionary(t => t.Id);

        var updated = 0;
        var errors = new List<string>();

        foreach (var u in request.Updates)
        {
            if (u.RentAmountMonthly <= 0)
            {
                errors.Add($"Tenancy {u.TenancyId}: RentAmountMonthly must be greater than 0.");
                continue;
            }

            if (!tenancies.TryGetValue(u.TenancyId, out var tenancy))
            {
                errors.Add($"Tenancy {u.TenancyId}: Not found or not in this organization.");
                continue;
            }

            tenancy.RentAmountMonthly = u.RentAmountMonthly;
            if (u.DepositAmount.HasValue && u.DepositAmount.Value >= 0)
                tenancy.DepositAmount = u.DepositAmount.Value;
            updated++;
        }

        if (updated > 0)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return new RentRepairResult { UpdatedCount = updated, Errors = errors };
    }

    private static TenancyDetailResponse MapToDetailResponse(Tenancy t)
    {
        return new TenancyDetailResponse
        {
            Id = t.Id,
            HouseId = t.HouseId,
            TenantId = t.TenantId,
            MoveInDate = t.MoveInDate,
            MoveOutDate = t.MoveOutDate,
            RentStartMonth = t.RentStartMonth,
            RentStartYear = t.RentStartYear,
            RentAmountMonthly = t.RentAmountMonthly,
            DepositAmount = t.DepositAmount,
            PaymentDueDay = t.PaymentDueDay,
            Status = t.Status ?? "Active",
            Notes = t.Notes,
            House = t.House == null ? null : new TenancyHouseDto { Id = t.House.Id, AddressLine1 = t.House.AddressLine1, AddressLine2 = t.House.AddressLine2, City = t.House.City, Postcode = t.House.Postcode, TotalRooms = t.House.TotalRooms, CreatedAt = t.House.CreatedAt },
            Tenant = t.Tenant == null ? null : new TenancyTenantDto { Id = t.Tenant.Id, FullName = t.Tenant.FullName, PhoneNumber = t.Tenant.PhoneNumber, Email = t.Tenant.Email, UniversityName = t.Tenant.UniversityName, CreatedAt = t.Tenant.CreatedAt, IsArchived = t.Tenant.IsArchived }
        };
    }

    private void LogAudit(string eventType, Guid organizationId, string targetType, string targetId)
    {
        if (!Guid.TryParse(_tenantContext.UserId, out var actorId))
            return;

        _auditLogger.Log(actorId, _tenantContext.CurrentRole ?? "Unknown", eventType,
            organizationId: organizationId, targetType: targetType, targetId: targetId,
            supportSessionId: _tenantContext.ActiveSupportSessionId);
    }
}
