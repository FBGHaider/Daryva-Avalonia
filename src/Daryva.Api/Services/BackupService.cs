using Daryva.Api.Dtos;
using Daryva.Api.Repositories.Interfaces;
using Daryva.Api.Security.Interfaces;
using Daryva.Api.Services.Interfaces;

namespace Daryva.Api.Services;

public class BackupService : IBackupService
{
    private readonly IHouseRepository _houseRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly ITenancyRepository _tenancyRepository;
    private readonly IExpenseRepository _expenseRepository;
    private readonly IDocumentRepository _documentRepository;
    private readonly IRentPaymentRepository _rentPaymentRepository;
    private readonly IDepositPaymentRepository _depositPaymentRepository;
    private readonly IDepositReturnRepository _depositReturnRepository;
    private readonly INotificationRepository _notificationRepository;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<BackupService> _logger;

    public BackupService(
        IHouseRepository houseRepository,
        ITenantRepository tenantRepository,
        ITenancyRepository tenancyRepository,
        IExpenseRepository expenseRepository,
        IDocumentRepository documentRepository,
        IRentPaymentRepository rentPaymentRepository,
        IDepositPaymentRepository depositPaymentRepository,
        IDepositReturnRepository depositReturnRepository,
        INotificationRepository notificationRepository,
        ITenantContext tenantContext,
        ILogger<BackupService> logger)
    {
        _houseRepository = houseRepository;
        _tenantRepository = tenantRepository;
        _tenancyRepository = tenancyRepository;
        _expenseRepository = expenseRepository;
        _documentRepository = documentRepository;
        _rentPaymentRepository = rentPaymentRepository;
        _depositPaymentRepository = depositPaymentRepository;
        _depositReturnRepository = depositReturnRepository;
        _notificationRepository = notificationRepository;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<BulkImportRequest> ExportAsync(CancellationToken cancellationToken = default)
    {
        var houses = await _houseRepository.GetAllAsync(includeArchived: true, cancellationToken);
        var tenants = await _tenantRepository.GetAllAsync(includeArchived: true, cancellationToken);
        var tenancies = await _tenancyRepository.GetAllAsync(cancellationToken);
        var documents = await _documentRepository.GetAllAsync(cancellationToken);
        var expenses = await _expenseRepository.GetAllAsync(includeArchived: true, cancellationToken);
        var rentPayments = await _rentPaymentRepository.GetAllAsync(cancellationToken);
        var depositPayments = await _depositPaymentRepository.GetAllAsync(cancellationToken);
        var depositReturns = await _depositReturnRepository.GetAllAsync(cancellationToken);
        var templates = await _notificationRepository.GetTemplatesAsync(channel: null, type: null, cancellationToken);
        var notifications = await _notificationRepository.QueryNotificationsAsync(new NotificationFilterRequest(), cancellationToken);
        var attempts = notifications.SelectMany(n => n.Attempts).ToList();

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
                CreatedAt = NormalizeToUtc(h.CreatedAt),
                IsArchived = h.IsArchived
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
                OldReceiptDocumentId = e.ReceiptDocumentId.HasValue && documentIdMap.TryGetValue(e.ReceiptDocumentId.Value, out var docId) ? docId : null,
                IsArchived = e.IsArchived
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
                CollectedBy = r.CollectedBy,
                IsVoided = r.IsVoided,
                PaidFromDeposit = r.PaidFromDeposit
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
                Notes = d.Notes,
                IsVoided = d.IsVoided
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

        _logger.LogInformation("Exported backup for org {OrgId}: {Houses} houses, {Tenants} tenants, {Tenancies} tenancies, {DepositReturns} deposit returns", _tenantContext.CurrentOrgId!.Value, export.Houses.Count, export.Tenants.Count, export.Tenancies.Count, export.DepositReturns.Count);
        return export;
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
