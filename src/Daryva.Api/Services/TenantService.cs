using Daryva.Api.Domain;
using Daryva.Api.Dtos;
using Daryva.Api.Repositories.Interfaces;
using Daryva.Api.Security.Interfaces;
using Daryva.Api.Services.Interfaces;

namespace Daryva.Api.Services;

/// <summary>
/// Implementation of ITenantService.
/// All queries are automatically filtered by OrgId via EF Core global query filters.
/// </summary>
public class TenantService : ITenantService
{
    private readonly ITenantRepository _tenantRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<TenantService> _logger;
    private readonly ITenantContext _tenantContext;
    private readonly IAuditLogger _auditLogger;

    public TenantService(
        ITenantRepository tenantRepository,
        IUnitOfWork unitOfWork,
        ILogger<TenantService> logger,
        ITenantContext tenantContext,
        IAuditLogger auditLogger)
    {
        _tenantRepository = tenantRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _tenantContext = tenantContext;
        _auditLogger = auditLogger;
    }

    public async Task<List<TenantResponse>> GetAllTenantsAsync(
        bool includeArchived = false,
        CancellationToken cancellationToken = default)
    {
        var tenants = await _tenantRepository.GetAllAsync(includeArchived, cancellationToken);
        return tenants.Select(MapToResponse).ToList();
    }

    public async Task<TenantResponse?> GetTenantByIdAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var tenant = await _tenantRepository.GetByIdAsync(tenantId, cancellationToken);
        return tenant == null ? null : MapToResponse(tenant);
    }

    public async Task<TenantResponse> CreateTenantAsync(
        CreateTenantRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.FullName))
            throw new ArgumentException("Full name is required.", nameof(request.FullName));

        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            OrganizationId = _tenantContext.CurrentOrgId!.Value,
            FullName = request.FullName,
            Email = request.Email ?? string.Empty,
            PhoneNumber = request.PhoneNumber ?? string.Empty,
            UniversityName = string.IsNullOrWhiteSpace(request.UniversityName) ? null : request.UniversityName.Trim(),
            CreatedAt = DateTime.UtcNow,
            IsArchived = false
        };

        _tenantRepository.Add(tenant);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new TenantResponse
        {
            Id = tenant.Id,
            FullName = tenant.FullName,
            Email = tenant.Email,
            PhoneNumber = tenant.PhoneNumber,
            UniversityName = tenant.UniversityName,
            CreatedAt = tenant.CreatedAt,
            IsArchived = tenant.IsArchived,
            AppUserId = tenant.AppUserId,
            InviteSentAt = tenant.InviteSentAt,
            InviteAcceptedAt = tenant.InviteAcceptedAt
        };
    }

    public async Task<TenantResponse?> UpdateTenantAsync(
        Guid tenantId,
        UpdateTenantRequest request,
        CancellationToken cancellationToken = default)
    {
        // Tracked, no includes -- avoids attaching a detached Tenancies/House graph on SaveChanges.
        var tenant = await _tenantRepository.GetTrackedByIdAsync(tenantId, cancellationToken);
        if (tenant == null)
            return null;

        tenant.FullName = request.FullName ?? tenant.FullName;
        tenant.Email = request.Email ?? tenant.Email;
        tenant.PhoneNumber = request.PhoneNumber ?? tenant.PhoneNumber;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new TenantResponse
        {
            Id = tenant.Id,
            FullName = tenant.FullName,
            Email = tenant.Email,
            PhoneNumber = tenant.PhoneNumber,
            CreatedAt = tenant.CreatedAt,
            IsArchived = tenant.IsArchived,
            AppUserId = tenant.AppUserId,
            InviteSentAt = tenant.InviteSentAt,
            InviteAcceptedAt = tenant.InviteAcceptedAt
        };
    }

    public async Task DeleteTenantAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var tenant = await _tenantRepository.GetTrackedByIdAsync(tenantId, cancellationToken);
        if (tenant != null)
        {
            _tenantRepository.Remove(tenant);
            LogAudit(AuditEventTypes.TenantDeleted, tenant.OrganizationId, nameof(Tenant), tenant.Id.ToString());
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<bool> ArchiveTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var tenant = await _tenantRepository.GetTrackedWithTenanciesByIdAsync(tenantId, cancellationToken);
        if (tenant == null)
            return false;

        var archiveDate = DateTime.UtcNow.Date;
        foreach (var tenancy in tenant.Tenancies)
        {
            if (string.Equals(tenancy.Status, "Active", StringComparison.OrdinalIgnoreCase)
                && (!tenancy.MoveOutDate.HasValue || tenancy.MoveOutDate.Value.Date > archiveDate))
            {
                tenancy.MoveOutDate = archiveDate;
                tenancy.Status = "Ended";
            }
        }

        tenant.IsArchived = true;
        LogAudit(AuditEventTypes.TenantArchived, tenant.OrganizationId, nameof(Tenant), tenant.Id.ToString());
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Archived tenant {TenantId}", tenantId);
        return true;
    }

    public async Task<bool> UnarchiveTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var tenant = await _tenantRepository.GetTrackedByIdAsync(tenantId, cancellationToken);
        if (tenant == null)
            return false;

        tenant.IsArchived = false;
        LogAudit(AuditEventTypes.TenantUnarchived, tenant.OrganizationId, nameof(Tenant), tenant.Id.ToString());
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Unarchived tenant {TenantId}", tenantId);
        return true;
    }

    /// <summary>
    /// "Current tenancy" is Status == "Active" (canonical -- matches TenancyRepository and
    /// ArchiveTenantAsync above), not a date comparison against MoveOutDate: dates can go stale
    /// or be entered before Status is updated, and Status is the field the rest of the tenancy
    /// lifecycle (EndTenancy/ReactivateTenancy) already treats as authoritative.
    /// </summary>
    private static TenantResponse MapToResponse(Tenant t)
    {
        var today = DateTime.UtcNow.Date;

        var currentTenancy = t.Tenancies
            .Where(te => te.Status == "Active")
            .OrderByDescending(te => te.MoveInDate)
            .FirstOrDefault();

        var leaveDate = t.Tenancies
            .Where(te => te.MoveOutDate.HasValue)
            .OrderByDescending(te => te.MoveOutDate)
            .Select(te => te.MoveOutDate)
            .FirstOrDefault();

        // A tenant can be archived without ever having a tenancy that was formally ended (e.g.
        // archived manually, or their only tenancy was deleted rather than ended) -- there's then
        // no real leave date to show. Fabricate "the 1st of this month" as a display placeholder
        // rather than leaving the UI's leave-date column blank for an archived tenant.
        if (t.IsArchived && !leaveDate.HasValue)
        {
            leaveDate = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        }

        return new TenantResponse
        {
            Id = t.Id,
            FullName = t.FullName,
            Email = t.Email,
            PhoneNumber = t.PhoneNumber,
            UniversityName = t.UniversityName,
            CreatedAt = t.CreatedAt,
            IsArchived = t.IsArchived,
            CurrentHouseAddress = currentTenancy?.House?.AddressLine1 ?? string.Empty,
            CurrentHouseId = currentTenancy?.HouseId,
            CurrentTenancyId = currentTenancy?.Id,
            LeaveDate = leaveDate,
            AppUserId = t.AppUserId,
            InviteSentAt = t.InviteSentAt,
            InviteAcceptedAt = t.InviteAcceptedAt
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
