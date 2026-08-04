using Daryva.Api.Data;
using Daryva.Api.Dtos;
using Daryva.Api.Security.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Daryva.Api.Controllers;

[ApiController]
[Route("api/backup")]
[Authorize]
public class BackupController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<BackupController> _logger;

    public BackupController(AppDbContext dbContext, ITenantContext tenantContext, ILogger<BackupController> logger)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    [HttpGet("export")]
    public async Task<ActionResult<BulkImportRequest>> Export(CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.CurrentOrgId.HasValue)
            return BadRequest(new { error = "Organization context not set." });

        var houses = await _dbContext.Houses.AsNoTracking().ToListAsync(cancellationToken);
        var tenants = await _dbContext.Tenants.AsNoTracking().ToListAsync(cancellationToken);
        var tenancies = await _dbContext.Tenancies.AsNoTracking().ToListAsync(cancellationToken);
        var documents = await _dbContext.Documents.AsNoTracking().ToListAsync(cancellationToken);
        var expenses = await _dbContext.Expenses.AsNoTracking().ToListAsync(cancellationToken);
        var rentPayments = await _dbContext.RentPayments.AsNoTracking().ToListAsync(cancellationToken);
        var depositPayments = await _dbContext.DepositPayments.AsNoTracking().ToListAsync(cancellationToken);
        var depositReturns = await _dbContext.DepositReturns.AsNoTracking().ToListAsync(cancellationToken);
        var templates = await _dbContext.NotificationTemplates.AsNoTracking().ToListAsync(cancellationToken);
        var notifications = await _dbContext.Notifications.AsNoTracking().ToListAsync(cancellationToken);
        var attempts = await _dbContext.NotificationAttempts.AsNoTracking().ToListAsync(cancellationToken);

        var houseIdMap = houses.Select((h, i) => new { h.Id, OldId = i + 1 }).ToDictionary(x => x.Id, x => x.OldId);
        var tenantIdMap = tenants.Select((t, i) => new { t.Id, OldId = i + 1 }).ToDictionary(x => x.Id, x => x.OldId);
        var tenancyIdMap = tenancies.Select((t, i) => new { t.Id, OldId = i + 1 }).ToDictionary(x => x.Id, x => x.OldId);
        var documentIdMap = documents.Select((d, i) => new { d.Id, OldId = i + 1 }).ToDictionary(x => x.Id, x => x.OldId);
        var templateIdMap = templates.Select((t, i) => new { t.Id, OldId = i + 1 }).ToDictionary(x => x.Id, x => x.OldId);
        var notificationIdMap = notifications.Select((n, i) => new { n.Id, OldId = i + 1 }).ToDictionary(x => x.Id, x => x.OldId);

        var export = new BulkImportRequest
        {
            Houses = houses.Select(h => new ImportHouse
            {
                OldId = houseIdMap[h.Id],
                Name = h.Name,
                AddressLine1 = h.AddressLine1,
                AddressLine2 = h.AddressLine2,
                City = h.City,
                Postcode = h.Postcode,
                TotalRooms = h.TotalRooms,
                CreatedAt = NormalizeToUtc(h.CreatedAt)
            }).ToList(),
            Tenants = tenants.Select(t => new ImportTenant
            {
                OldId = tenantIdMap[t.Id],
                FullName = t.FullName,
                PhoneNumber = t.PhoneNumber,
                Email = t.Email,
                UniversityName = t.UniversityName,
                CreatedAt = NormalizeToUtc(t.CreatedAt),
                IsArchived = t.IsArchived
            }).ToList(),
            Tenancies = tenancies.Select(t => new ImportTenancy
            {
                OldId = tenancyIdMap[t.Id],
                OldHouseId = houseIdMap[t.HouseId],
                OldTenantId = tenantIdMap[t.TenantId],
                MoveInDate = NormalizeToUtc(t.MoveInDate),
                MoveOutDate = NormalizeToUtc(t.MoveOutDate),
                RentStartMonth = t.RentStartMonth,
                RentStartYear = t.RentStartYear,
                RentAmountMonthly = t.RentAmountMonthly,
                DepositAmount = t.DepositAmount,
                PaymentDueDay = t.PaymentDueDay,
                Status = t.Status,
                Notes = t.Notes
            }).ToList(),
            Expenses = expenses.Select(e => new ImportExpense
            {
                OldId = e.Id.GetHashCode(),
                OldHouseId = houseIdMap[e.HouseId],
                DateIncurred = NormalizeToUtc(e.DateIncurred),
                Category = e.Category,
                Amount = e.Amount,
                Vendor = e.Vendor,
                Notes = e.Notes,
                OldReceiptDocumentId = e.ReceiptDocumentId.HasValue && documentIdMap.TryGetValue(e.ReceiptDocumentId.Value, out var docId) ? docId : null
            }).ToList(),
            Documents = documents.Select(d => new ImportDocument
            {
                OldId = documentIdMap[d.Id],
                OldTenantId = d.TenantId.HasValue && tenantIdMap.TryGetValue(d.TenantId.Value, out var tenantId) ? tenantId : null,
                OldTenancyId = d.TenancyId.HasValue && tenancyIdMap.TryGetValue(d.TenancyId.Value, out var tenancyId) ? tenancyId : null,
                OldHouseId = d.HouseId.HasValue && houseIdMap.TryGetValue(d.HouseId.Value, out var houseId) ? houseId : null,
                Type = d.Type,
                DisplayName = d.DisplayName,
                FileName = d.FileName,
                FileMimeType = d.FileMimeType,
                StoragePath = d.StoragePath,
                Source = d.Source,
                UploadedAt = NormalizeToUtc(d.UploadedAt),
                ValidFrom = NormalizeToUtc(d.ValidFrom),
                ValidTo = NormalizeToUtc(d.ValidTo),
                Version = d.Version,
                IsActive = d.IsActive
            }).ToList(),
            RentPayments = rentPayments.Select(r => new ImportRentPayment
            {
                OldId = r.Id.GetHashCode(),
                OldTenancyId = tenancyIdMap[r.TenancyId],
                DatePaid = NormalizeToUtc(r.DatePaid),
                AmountPaid = r.AmountPaid,
                PaymentMethod = r.PaymentMethod,
                ReferenceNumber = r.ReferenceNumber,
                Notes = r.Notes,
                CollectedBy = r.CollectedBy
            }).ToList(),
            DepositPayments = depositPayments.Select(d => new ImportDepositPayment
            {
                OldId = d.Id.GetHashCode(),
                OldTenancyId = tenancyIdMap[d.TenancyId],
                DatePaid = NormalizeToUtc(d.DatePaid),
                AmountPaid = d.AmountPaid,
                PaymentMethod = d.PaymentMethod,
                ProtectionScheme = d.ProtectionScheme,
                ProtectionReference = d.ProtectionReference,
                Notes = d.Notes
            }).ToList(),
            DepositReturns = depositReturns.Select(dr => new ImportDepositReturn
            {
                OldId = dr.Id.GetHashCode(),
                OldTenancyId = tenancyIdMap[dr.TenancyId],
                ReturnedDate = NormalizeToUtc(dr.ReturnedDate),
                AmountReturned = dr.AmountReturned,
                Notes = dr.Notes
            }).ToList(),
            NotificationTemplates = templates.Select(t => new ImportNotificationTemplate
            {
                OldId = templateIdMap[t.Id],
                Name = t.Name,
                Channel = t.Channel,
                Type = t.Type,
                SubjectTemplate = t.SubjectTemplate,
                BodyTemplate = t.BodyTemplate,
                IsDefault = t.IsDefault,
                CreatedAt = NormalizeToUtc(t.CreatedAt)
            }).ToList(),
            Notifications = notifications.Select(n => new ImportNotification
            {
                OldId = notificationIdMap[n.Id],
                OldTenantId = tenantIdMap[n.TenantId],
                OldTenancyId = n.TenancyId.HasValue && tenancyIdMap.TryGetValue(n.TenancyId.Value, out var tenancyId) ? tenancyId : null,
                Channel = n.Channel,
                Type = n.Type,
                ToAddress = n.ToAddress,
                Subject = n.Subject,
                Body = n.Body,
                ScheduledFor = NormalizeToUtc(n.ScheduledFor),
                SentAt = NormalizeToUtc(n.SentAt),
                Status = n.Status,
                ProviderMessageId = n.ProviderMessageId,
                Error = n.Error,
                OldTemplateId = n.TemplateId.HasValue && templateIdMap.TryGetValue(n.TemplateId.Value, out var templateId) ? templateId : null
            }).ToList(),
            NotificationAttempts = attempts.Select(a => new ImportNotificationAttempt
            {
                OldId = a.Id.GetHashCode(),
                OldNotificationId = notificationIdMap[a.NotificationId],
                AttemptedAt = NormalizeToUtc(a.AttemptedAt),
                Status = a.Status,
                Error = a.Error,
                ProviderMessageId = a.ProviderMessageId
            }).ToList()
        };

        _logger.LogInformation("Exported backup for org {OrgId}: {Houses} houses, {Tenants} tenants, {Tenancies} tenancies, {DepositReturns} deposit returns", _tenantContext.CurrentOrgId.Value, export.Houses.Count, export.Tenants.Count, export.Tenancies.Count, export.DepositReturns.Count);
        return Ok(export);
    }

    private static DateTime NormalizeToUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }

    private static DateTime? NormalizeToUtc(DateTime? value)
    {
        return value.HasValue ? NormalizeToUtc(value.Value) : null;
    }
}
