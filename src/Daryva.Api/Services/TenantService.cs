using Daryva.Api.Domain;
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

    public async Task<IEnumerable<Tenant>> GetAllTenantsAsync(
        bool includeArchived = false,
        CancellationToken cancellationToken = default)
        => await _tenantRepository.GetAllAsync(includeArchived, cancellationToken);

    public async Task<Tenant?> GetTenantByIdAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
        => await _tenantRepository.GetByIdAsync(tenantId, cancellationToken);

    public async Task<Tenant> CreateTenantAsync(
        Tenant tenant,
        CancellationToken cancellationToken = default)
    {
        _tenantRepository.Add(tenant);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return tenant;
    }

    public async Task UpdateTenantAsync(
        Tenant tenant,
        CancellationToken cancellationToken = default)
    {
        // Load tracked entity only; do not attach the detached graph (Tenancies/House) to avoid 500 on SaveChanges
        var existing = await _tenantRepository.GetTrackedByIdAsync(tenant.Id, cancellationToken);
        if (existing == null)
            return;

        existing.FullName = tenant.FullName;
        existing.Email = tenant.Email ?? string.Empty;
        existing.PhoneNumber = tenant.PhoneNumber ?? string.Empty;
        existing.UniversityName = tenant.UniversityName;

        await _unitOfWork.SaveChangesAsync(cancellationToken);
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

    private void LogAudit(string eventType, Guid organizationId, string targetType, string targetId)
    {
        if (!Guid.TryParse(_tenantContext.UserId, out var actorId))
            return;

        _auditLogger.Log(actorId, _tenantContext.CurrentRole ?? "Unknown", eventType,
            organizationId: organizationId, targetType: targetType, targetId: targetId,
            supportSessionId: _tenantContext.ActiveSupportSessionId);
    }
}
