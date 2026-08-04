using Daryva.Api.Data;
using Daryva.Api.Domain;
using Daryva.Api.Dtos;
using Daryva.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Daryva.Api.Services;

public class BulkImportService : IBulkImportService
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<BulkImportService> _logger;

    public BulkImportService(AppDbContext dbContext, ILogger<BulkImportService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    private async Task SaveWithErrorRecoveryAsync(AppDbContext dbContext, string entityType, List<string> errors, ILogger logger, CancellationToken cancellationToken = default)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "Error saving {EntityType}: {Message}", entityType, ex.InnerException?.Message ?? ex.Message);
            
            // Try to identify and remove problematic entries
            var failedEntries = dbContext.ChangeTracker.Entries()
                .Where(e => e.State == EntityState.Added)
                .ToList();

            foreach (var entry in failedEntries)
            {
                errors.Add($"{entityType} failed validation: {entry.Entity?.GetType().Name}");
                dbContext.Entry(entry.Entity!).State = EntityState.Detached;
            }

            // Try saving again without the problematic entries
            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex2)
            {
                logger.LogError(ex2, "Second save attempt also failed for {EntityType}", entityType);
                errors.Add($"Fatal error saving {entityType}: {ex2.InnerException?.Message ?? ex2.Message}");
            }
        }
    }

    public async Task<BulkImportResponse> ImportDataAsync(Guid organizationId, BulkImportRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Starting bulk import for organization {OrgId}: {Houses} houses, {Tenants} tenants, {Tenancies} tenancies, {Expenses} expenses, {Documents} documents, {RentPayments} rent payments, {DepositPayments} deposit payments, {DepositReturns} deposit returns, {NotificationTemplates} templates, {Notifications} notifications, {NotificationAttempts} attempts",
            organizationId,
            request.Houses.Count,
            request.Tenants.Count,
            request.Tenancies.Count,
            request.Expenses.Count,
            request.Documents.Count,
            request.RentPayments.Count,
            request.DepositPayments.Count,
            request.DepositReturns.Count,
            request.NotificationTemplates.Count,
            request.Notifications.Count,
            request.NotificationAttempts.Count);

        var response = new BulkImportResponse { Success = true };
        var errors = new List<string>();
        
        // Maps: oldId -> newGuid
        var houseIdMap = new Dictionary<int, Guid>();
        var tenantIdMap = new Dictionary<int, Guid>();
        var tenancyIdMap = new Dictionary<int, Guid>();
        var documentIdMap = new Dictionary<int, Guid>();
        var rentPaymentIdMap = new Dictionary<int, Guid>();
        var depositPaymentIdMap = new Dictionary<int, Guid>();
        var templateIdMap = new Dictionary<int, Guid>();
        var notificationIdMap = new Dictionary<int, Guid>();

        using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            // ========== 1. IMPORT HOUSES ==========
            foreach (var importHouse in request.Houses)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(importHouse.AddressLine1))
                    {
                        errors.Add($"House {importHouse.OldId}: AddressLine1 is required");
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(importHouse.City))
                    {
                        errors.Add($"House {importHouse.OldId}: City is required");
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(importHouse.Postcode))
                    {
                        errors.Add($"House {importHouse.OldId}: Postcode is required");
                        continue;
                    }

                    // Check if house already exists (by address)
                    var addressLine1 = importHouse.AddressLine1.Trim();
                    var city = importHouse.City.Trim();
                    var postcode = importHouse.Postcode.Trim();

                    var existingHouse = await _dbContext.Houses
                        .AsNoTracking()
                        .FirstOrDefaultAsync(h => 
                            h.OrganizationId == organizationId &&
                            h.AddressLine1 == addressLine1 &&
                            h.City == city &&
                            h.Postcode == postcode,
                            cancellationToken);

                    if (existingHouse != null)
                    {
                        if (existingHouse.TotalRooms != importHouse.TotalRooms)
                        {
                            var trackedExisting = await _dbContext.Houses
                                .FirstOrDefaultAsync(h => h.Id == existingHouse.Id, cancellationToken);
                            if (trackedExisting != null)
                            {
                                trackedExisting.TotalRooms = importHouse.TotalRooms;
                            }
                        }

                        _logger.LogInformation("House already exists: {Address}, {City}, {Postcode}", addressLine1, city, postcode);
                        houseIdMap[importHouse.OldId] = existingHouse.Id;
                        continue;
                    }

                    // Use address as name if name is missing
                    var houseName = string.IsNullOrWhiteSpace(importHouse.Name) 
                        ? addressLine1
                        : importHouse.Name.Trim();

                    var house = new House
                    {
                        Id = Guid.NewGuid(),
                        OrganizationId = organizationId,
                        Name = houseName,
                        AddressLine1 = addressLine1,
                        AddressLine2 = string.IsNullOrWhiteSpace(importHouse.AddressLine2) ? null : importHouse.AddressLine2.Trim(),
                        City = city,
                        Postcode = postcode,
                        TotalRooms = importHouse.TotalRooms,
                        CreatedAt = importHouse.CreatedAt > DateTime.MinValue ? importHouse.CreatedAt : DateTime.UtcNow
                    };

                    _dbContext.Houses.Add(house);
                    houseIdMap[importHouse.OldId] = house.Id;
                    response.Stats.HousesImported++;
                }
                catch (Exception ex)
                {
                    errors.Add($"House {importHouse.OldId}: {ex.Message}");
                }
            }

            await SaveWithErrorRecoveryAsync(_dbContext, "Houses", errors, _logger, cancellationToken);
            _logger.LogInformation("Imported {Count} houses", response.Stats.HousesImported);

            // ========== 2. IMPORT TENANTS ==========
            foreach (var importTenant in request.Tenants)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(importTenant.FullName))
                    {
                        errors.Add($"Tenant {importTenant.OldId}: FullName is required");
                        continue;
                    }

                    // Check if tenant already exists (by email if available, otherwise by name)
                    Tenant? existingTenant = null;

                    if (!string.IsNullOrWhiteSpace(importTenant.Email))
                    {
                        var email = importTenant.Email.Trim().ToLower();
                        existingTenant = await _dbContext.Tenants
                            .AsNoTracking()
                            .FirstOrDefaultAsync(t => 
                                t.OrganizationId == organizationId &&
                                t.Email != null &&
                                t.Email.ToLower() == email,
                                cancellationToken);

                        if (existingTenant != null)
                        {
                            _logger.LogInformation("Tenant already exists with email: {Email}", email);
                            tenantIdMap[importTenant.OldId] = existingTenant.Id;
                            continue;
                        }
                    }

                    // If no email match, check by name (case-insensitive)
                    var fullName = importTenant.FullName.Trim();
                    existingTenant = await _dbContext.Tenants
                        .AsNoTracking()
                        .FirstOrDefaultAsync(t => 
                            t.OrganizationId == organizationId &&
                            t.FullName.ToLower() == fullName.ToLower(),
                            cancellationToken);

                    if (existingTenant != null)
                    {
                        _logger.LogInformation("Tenant already exists with name: {Name}", fullName);
                        tenantIdMap[importTenant.OldId] = existingTenant.Id;
                        continue;
                    }

                    var tenant = new Tenant
                    {
                        Id = Guid.NewGuid(),
                        OrganizationId = organizationId,
                        FullName = fullName,
                        PhoneNumber = string.IsNullOrWhiteSpace(importTenant.PhoneNumber) ? string.Empty : importTenant.PhoneNumber.Trim(),
                        Email = string.IsNullOrWhiteSpace(importTenant.Email) ? string.Empty : importTenant.Email.Trim(),
                        UniversityName = string.IsNullOrWhiteSpace(importTenant.UniversityName) ? null : importTenant.UniversityName.Trim(),
                        CreatedAt = importTenant.CreatedAt > DateTime.MinValue ? importTenant.CreatedAt : DateTime.UtcNow,
                        IsArchived = importTenant.IsArchived
                    };

                    _dbContext.Tenants.Add(tenant);
                    tenantIdMap[importTenant.OldId] = tenant.Id;
                    response.Stats.TenantsImported++;
                }
                catch (Exception ex)
                {
                    errors.Add($"Tenant {importTenant.OldId}: {ex.Message}");
                }
            }

            await SaveWithErrorRecoveryAsync(_dbContext, "Tenants", errors, _logger, cancellationToken);
            _logger.LogInformation("Imported {Count} tenants", response.Stats.TenantsImported);

            // ========== 3. IMPORT TENANCIES ==========
            foreach (var importTenancy in request.Tenancies)
            {
                try
                {
                    // Validate required fields
                    if (importTenancy.MoveInDate == default)
                    {
                        errors.Add($"Tenancy {importTenancy.OldId}: MoveInDate is required");
                        continue;
                    }

                    if (importTenancy.RentAmountMonthly <= 0)
                    {
                        errors.Add($"Tenancy {importTenancy.OldId}: RentAmountMonthly must be greater than 0");
                        continue;
                    }

                    if (importTenancy.PaymentDueDay < 1 || importTenancy.PaymentDueDay > 31)
                    {
                        errors.Add($"Tenancy {importTenancy.OldId}: PaymentDueDay must be between 1 and 31");
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(importTenancy.Status))
                    {
                        errors.Add($"Tenancy {importTenancy.OldId}: Status is required");
                        continue;
                    }

                    if (!houseIdMap.TryGetValue(importTenancy.OldHouseId, out var houseId))
                    {
                        errors.Add($"Tenancy {importTenancy.OldId}: House ID {importTenancy.OldHouseId} not found");
                        continue;
                    }

                    if (!tenantIdMap.TryGetValue(importTenancy.OldTenantId, out var tenantId))
                    {
                        errors.Add($"Tenancy {importTenancy.OldId}: Tenant ID {importTenancy.OldTenantId} not found");
                        continue;
                    }

                    var tenancy = new Tenancy
                    {
                        Id = Guid.NewGuid(),
                        OrganizationId = organizationId,
                        HouseId = houseId,
                        TenantId = tenantId,
                        MoveInDate = importTenancy.MoveInDate,
                        MoveOutDate = importTenancy.MoveOutDate,
                        RentStartMonth = importTenancy.RentStartMonth,
                        RentStartYear = importTenancy.RentStartYear,
                        RentAmountMonthly = importTenancy.RentAmountMonthly,
                        DepositAmount = importTenancy.DepositAmount,
                        PaymentDueDay = importTenancy.PaymentDueDay,
                        Status = importTenancy.Status.Trim(),
                        Notes = string.IsNullOrWhiteSpace(importTenancy.Notes) ? null : importTenancy.Notes.Trim()
                    };

                    _dbContext.Tenancies.Add(tenancy);
                    tenancyIdMap[importTenancy.OldId] = tenancy.Id;
                    response.Stats.TenanciesImported++;
                }
                catch (Exception ex)
                {
                    errors.Add($"Tenancy {importTenancy.OldId}: {ex.Message}");
                }
            }

            await SaveWithErrorRecoveryAsync(_dbContext, "Tenancies", errors, _logger, cancellationToken);
            _logger.LogInformation("Imported {Count} tenancies", response.Stats.TenanciesImported);

            // ========== 4. IMPORT DOCUMENTS ==========
            foreach (var importDoc in request.Documents)
            {
                try
                {
                    // Validate required fields
                    if (string.IsNullOrWhiteSpace(importDoc.Type))
                    {
                        errors.Add($"Document {importDoc.OldId}: Type is required");
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(importDoc.DisplayName))
                    {
                        errors.Add($"Document {importDoc.OldId}: DisplayName is required");
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(importDoc.FileName))
                    {
                        errors.Add($"Document {importDoc.OldId}: FileName is required");
                        continue;
                    }

                    if (importDoc.UploadedAt == default)
                    {
                        errors.Add($"Document {importDoc.OldId}: UploadedAt is required");
                        continue;
                    }

                    Guid? houseId = importDoc.OldHouseId.HasValue && houseIdMap.TryGetValue(importDoc.OldHouseId.Value, out var h) ? h : null;
                    Guid? tenantId = importDoc.OldTenantId.HasValue && tenantIdMap.TryGetValue(importDoc.OldTenantId.Value, out var t) ? t : null;
                    Guid? tenancyId = importDoc.OldTenancyId.HasValue && tenancyIdMap.TryGetValue(importDoc.OldTenancyId.Value, out var tn) ? tn : null;

                    var document = new Document
                    {
                        Id = Guid.NewGuid(),
                        OrganizationId = organizationId,
                        HouseId = houseId,
                        TenantId = tenantId,
                        TenancyId = tenancyId,
                        Type = importDoc.Type.Trim(),
                        DisplayName = importDoc.DisplayName.Trim(),
                        FileName = importDoc.FileName.Trim(),
                        FileMimeType = string.IsNullOrWhiteSpace(importDoc.FileMimeType) ? null : importDoc.FileMimeType.Trim(),
                        StoragePath = string.IsNullOrWhiteSpace(importDoc.StoragePath) ? null : importDoc.StoragePath.Trim(),
                        Source = string.IsNullOrWhiteSpace(importDoc.Source) ? null : importDoc.Source.Trim(),
                        UploadedAt = importDoc.UploadedAt,
                        ValidFrom = importDoc.ValidFrom,
                        ValidTo = importDoc.ValidTo,
                        Version = Math.Max(importDoc.Version, 1),
                        IsActive = importDoc.IsActive
                    };

                    _dbContext.Documents.Add(document);
                    documentIdMap[importDoc.OldId] = document.Id;
                    response.Stats.DocumentsImported++;
                }
                catch (Exception ex)
                {
                    errors.Add($"Document {importDoc.OldId}: {ex.Message}");
                }
            }

            await SaveWithErrorRecoveryAsync(_dbContext, "Documents", errors, _logger, cancellationToken);
            _logger.LogInformation("Imported {Count} documents", response.Stats.DocumentsImported);

            // ========== 5. IMPORT EXPENSES ==========
            foreach (var importExpense in request.Expenses)
            {
                try
                {
                    // Validate required fields
                    if (string.IsNullOrWhiteSpace(importExpense.Category))
                    {
                        errors.Add($"Expense {importExpense.OldId}: Category is required");
                        continue;
                    }

                    if (importExpense.Amount <= 0)
                    {
                        errors.Add($"Expense {importExpense.OldId}: Amount must be greater than 0");
                        continue;
                    }

                    if (importExpense.DateIncurred == default)
                    {
                        errors.Add($"Expense {importExpense.OldId}: DateIncurred is required");
                        continue;
                    }

                    if (!houseIdMap.TryGetValue(importExpense.OldHouseId, out var houseId))
                    {
                        errors.Add($"Expense {importExpense.OldId}: House ID {importExpense.OldHouseId} not found");
                        continue;
                    }

                    Guid? receiptDocId = importExpense.OldReceiptDocumentId.HasValue && documentIdMap.TryGetValue(importExpense.OldReceiptDocumentId.Value, out var d) ? d : null;

                    var expense = new Expense
                    {
                        Id = Guid.NewGuid(),
                        OrganizationId = organizationId,
                        HouseId = houseId,
                        DateIncurred = importExpense.DateIncurred,
                        Category = importExpense.Category.Trim(),
                        Amount = importExpense.Amount,
                        Vendor = string.IsNullOrWhiteSpace(importExpense.Vendor) ? null : importExpense.Vendor.Trim(),
                        Notes = string.IsNullOrWhiteSpace(importExpense.Notes) ? null : importExpense.Notes.Trim(),
                        ReceiptDocumentId = receiptDocId
                    };

                    _dbContext.Expenses.Add(expense);
                    response.Stats.ExpensesImported++;
                }
                catch (Exception ex)
                {
                    errors.Add($"Expense {importExpense.OldId}: {ex.Message}");
                }
            }

            await SaveWithErrorRecoveryAsync(_dbContext, "Expenses", errors, _logger, cancellationToken);
            _logger.LogInformation("Imported {Count} expenses", response.Stats.ExpensesImported);

            // ========== 6. IMPORT RENT PAYMENTS ==========
            foreach (var importPayment in request.RentPayments)
            {
                try
                {
                    if (importPayment.AmountPaid <= 0)
                    {
                        errors.Add($"Rent Payment {importPayment.OldId}: Amount must be greater than 0");
                        continue;
                    }

                    if (importPayment.DatePaid == default)
                    {
                        errors.Add($"Rent Payment {importPayment.OldId}: DatePaid is required");
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(importPayment.PaymentMethod))
                    {
                        errors.Add($"Rent Payment {importPayment.OldId}: PaymentMethod is required");
                        continue;
                    }

                    if (!tenancyIdMap.TryGetValue(importPayment.OldTenancyId, out var tenancyId))
                    {
                        errors.Add($"Rent Payment {importPayment.OldId}: Tenancy ID {importPayment.OldTenancyId} not found");
                        continue;
                    }

                    var payment = new RentPayment
                    {
                        Id = Guid.NewGuid(),
                        OrganizationId = organizationId,
                        TenancyId = tenancyId,
                        DatePaid = importPayment.DatePaid,
                        AmountPaid = importPayment.AmountPaid,
                        PaymentMethod = importPayment.PaymentMethod.Trim(),
                        ReferenceNumber = string.IsNullOrWhiteSpace(importPayment.ReferenceNumber) ? null : importPayment.ReferenceNumber.Trim(),
                        Notes = string.IsNullOrWhiteSpace(importPayment.Notes) ? null : importPayment.Notes.Trim(),
                        CollectedBy = string.IsNullOrWhiteSpace(importPayment.CollectedBy) ? null : importPayment.CollectedBy.Trim()
                    };

                    _dbContext.RentPayments.Add(payment);
                    rentPaymentIdMap[importPayment.OldId] = payment.Id;
                    response.Stats.RentPaymentsImported++;
                }
                catch (Exception ex)
                {
                    errors.Add($"Rent Payment {importPayment.OldId}: {ex.Message}");
                }
            }

            await SaveWithErrorRecoveryAsync(_dbContext, "RentPayments", errors, _logger, cancellationToken);
            _logger.LogInformation("Imported {Count} rent payments", response.Stats.RentPaymentsImported);

            // ========== 7. IMPORT DEPOSIT PAYMENTS ==========
            foreach (var importDeposit in request.DepositPayments)
            {
                try
                {
                    if (importDeposit.AmountPaid <= 0)
                    {
                        errors.Add($"Deposit Payment {importDeposit.OldId}: Amount must be greater than 0");
                        continue;
                    }

                    if (importDeposit.DatePaid == default)
                    {
                        errors.Add($"Deposit Payment {importDeposit.OldId}: DatePaid is required");
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(importDeposit.PaymentMethod))
                    {
                        errors.Add($"Deposit Payment {importDeposit.OldId}: PaymentMethod is required");
                        continue;
                    }

                    if (!tenancyIdMap.TryGetValue(importDeposit.OldTenancyId, out var tenancyId))
                    {
                        errors.Add($"Deposit Payment {importDeposit.OldId}: Tenancy ID {importDeposit.OldTenancyId} not found");
                        continue;
                    }

                    var deposit = new DepositPayment
                    {
                        Id = Guid.NewGuid(),
                        OrganizationId = organizationId,
                        TenancyId = tenancyId,
                        DatePaid = importDeposit.DatePaid,
                        AmountPaid = importDeposit.AmountPaid,
                        PaymentMethod = importDeposit.PaymentMethod.Trim(),
                        ProtectionScheme = string.IsNullOrWhiteSpace(importDeposit.ProtectionScheme) ? null : importDeposit.ProtectionScheme.Trim(),
                        ProtectionReference = string.IsNullOrWhiteSpace(importDeposit.ProtectionReference) ? null : importDeposit.ProtectionReference.Trim(),
                        Notes = string.IsNullOrWhiteSpace(importDeposit.Notes) ? null : importDeposit.Notes.Trim()
                    };

                    _dbContext.DepositPayments.Add(deposit);
                    depositPaymentIdMap[importDeposit.OldId] = deposit.Id;
                    response.Stats.DepositPaymentsImported++;
                }
                catch (Exception ex)
                {
                    errors.Add($"Deposit Payment {importDeposit.OldId}: {ex.Message}");
                }
            }

            await SaveWithErrorRecoveryAsync(_dbContext, "DepositPayments", errors, _logger, cancellationToken);
            _logger.LogInformation("Imported {Count} deposit payments", response.Stats.DepositPaymentsImported);

            // ========== 7a. IMPORT DEPOSIT RETURNS ==========
            foreach (var importReturn in request.DepositReturns)
            {
                try
                {
                    if (importReturn.AmountReturned < 0)
                    {
                        errors.Add($"Deposit Return {importReturn.OldId}: AmountReturned cannot be negative");
                        continue;
                    }

                    if (importReturn.ReturnedDate == default)
                    {
                        errors.Add($"Deposit Return {importReturn.OldId}: ReturnedDate is required");
                        continue;
                    }

                    if (!tenancyIdMap.TryGetValue(importReturn.OldTenancyId, out var tenancyId))
                    {
                        errors.Add($"Deposit Return {importReturn.OldId}: Tenancy ID {importReturn.OldTenancyId} not found");
                        continue;
                    }

                    var depositReturn = new DepositReturn
                    {
                        Id = Guid.NewGuid(),
                        OrganizationId = organizationId,
                        TenancyId = tenancyId,
                        ReturnedDate = importReturn.ReturnedDate,
                        AmountReturned = importReturn.AmountReturned,
                        Notes = string.IsNullOrWhiteSpace(importReturn.Notes) ? null : importReturn.Notes.Trim()
                    };

                    _dbContext.DepositReturns.Add(depositReturn);
                    response.Stats.DepositReturnsImported++;
                }
                catch (Exception ex)
                {
                    errors.Add($"Deposit Return {importReturn.OldId}: {ex.Message}");
                }
            }

            await SaveWithErrorRecoveryAsync(_dbContext, "DepositReturns", errors, _logger, cancellationToken);
            _logger.LogInformation("Imported {Count} deposit returns", response.Stats.DepositReturnsImported);

            // ========== 8. IMPORT NOTIFICATION TEMPLATES ==========
            foreach (var importTemplate in request.NotificationTemplates)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(importTemplate.Name))
                    {
                        errors.Add($"NotificationTemplate {importTemplate.OldId}: Name is required");
                        continue;
                    }

                    var template = new NotificationTemplate
                    {
                        Id = Guid.NewGuid(),
                        OrganizationId = organizationId,
                        Name = importTemplate.Name.Trim(),
                        Channel = importTemplate.Channel.Trim(),
                        Type = importTemplate.Type.Trim(),
                        SubjectTemplate = string.IsNullOrWhiteSpace(importTemplate.SubjectTemplate) ? null : importTemplate.SubjectTemplate.Trim(),
                        BodyTemplate = importTemplate.BodyTemplate,
                        IsDefault = importTemplate.IsDefault,
                        CreatedAt = importTemplate.CreatedAt == default ? DateTime.UtcNow : importTemplate.CreatedAt
                    };

                    _dbContext.NotificationTemplates.Add(template);
                    templateIdMap[importTemplate.OldId] = template.Id;
                    response.Stats.NotificationTemplatesImported++;
                }
                catch (Exception ex)
                {
                    errors.Add($"NotificationTemplate {importTemplate.OldId}: {ex.Message}");
                }
            }

            await SaveWithErrorRecoveryAsync(_dbContext, "NotificationTemplates", errors, _logger, cancellationToken);
            _logger.LogInformation("Imported {Count} notification templates", response.Stats.NotificationTemplatesImported);

            // ========== 9. IMPORT NOTIFICATIONS ==========
            foreach (var importNotification in request.Notifications)
            {
                try
                {
                    if (!tenantIdMap.TryGetValue(importNotification.OldTenantId, out var tenantId))
                    {
                        errors.Add($"Notification {importNotification.OldId}: Tenant ID {importNotification.OldTenantId} not found");
                        continue;
                    }

                    Guid? tenancyId = null;
                    if (importNotification.OldTenancyId.HasValue)
                    {
                        if (tenancyIdMap.TryGetValue(importNotification.OldTenancyId.Value, out var mappedTenancyId))
                        {
                            tenancyId = mappedTenancyId;
                        }
                    }

                    Guid? templateId = null;
                    if (importNotification.OldTemplateId.HasValue)
                    {
                        if (templateIdMap.TryGetValue(importNotification.OldTemplateId.Value, out var mappedTemplateId))
                        {
                            templateId = mappedTemplateId;
                        }
                    }

                    var notification = new Notification
                    {
                        Id = Guid.NewGuid(),
                        OrganizationId = organizationId,
                        TenantId = tenantId,
                        TenancyId = tenancyId,
                        Channel = importNotification.Channel.Trim(),
                        Type = importNotification.Type.Trim(),
                        ToAddress = importNotification.ToAddress.Trim(),
                        Subject = string.IsNullOrWhiteSpace(importNotification.Subject) ? null : importNotification.Subject.Trim(),
                        Body = importNotification.Body,
                        ScheduledFor = importNotification.ScheduledFor,
                        SentAt = importNotification.SentAt,
                        Status = string.IsNullOrWhiteSpace(importNotification.Status) ? "Pending" : importNotification.Status.Trim(),
                        ProviderMessageId = string.IsNullOrWhiteSpace(importNotification.ProviderMessageId) ? null : importNotification.ProviderMessageId.Trim(),
                        Error = string.IsNullOrWhiteSpace(importNotification.Error) ? null : importNotification.Error.Trim(),
                        TemplateId = templateId
                    };

                    _dbContext.Notifications.Add(notification);
                    notificationIdMap[importNotification.OldId] = notification.Id;
                    response.Stats.NotificationsImported++;
                }
                catch (Exception ex)
                {
                    errors.Add($"Notification {importNotification.OldId}: {ex.Message}");
                }
            }

            await SaveWithErrorRecoveryAsync(_dbContext, "Notifications", errors, _logger, cancellationToken);
            _logger.LogInformation("Imported {Count} notifications", response.Stats.NotificationsImported);

            // ========== 10. IMPORT NOTIFICATION ATTEMPTS ==========
            foreach (var importAttempt in request.NotificationAttempts)
            {
                try
                {
                    if (!notificationIdMap.TryGetValue(importAttempt.OldNotificationId, out var notificationId))
                    {
                        errors.Add($"NotificationAttempt {importAttempt.OldId}: Notification ID {importAttempt.OldNotificationId} not found");
                        continue;
                    }

                    var attempt = new NotificationAttempt
                    {
                        Id = Guid.NewGuid(),
                        OrganizationId = organizationId,
                        NotificationId = notificationId,
                        AttemptedAt = importAttempt.AttemptedAt == default ? DateTime.UtcNow : importAttempt.AttemptedAt,
                        Status = string.IsNullOrWhiteSpace(importAttempt.Status) ? "Failed" : importAttempt.Status.Trim(),
                        Error = string.IsNullOrWhiteSpace(importAttempt.Error) ? null : importAttempt.Error.Trim(),
                        ProviderMessageId = string.IsNullOrWhiteSpace(importAttempt.ProviderMessageId) ? null : importAttempt.ProviderMessageId.Trim()
                    };

                    _dbContext.NotificationAttempts.Add(attempt);
                    response.Stats.NotificationAttemptsImported++;
                }
                catch (Exception ex)
                {
                    errors.Add($"NotificationAttempt {importAttempt.OldId}: {ex.Message}");
                }
            }

            await SaveWithErrorRecoveryAsync(_dbContext, "NotificationAttempts", errors, _logger, cancellationToken);
            _logger.LogInformation("Imported {Count} notification attempts", response.Stats.NotificationAttemptsImported);

            // ========== COMMIT TRANSACTION ==========
            await transaction.CommitAsync(cancellationToken);

            response.Stats.TotalItemsImported =
                response.Stats.HousesImported +
                response.Stats.TenantsImported +
                response.Stats.TenanciesImported +
                response.Stats.ExpensesImported +
                response.Stats.DocumentsImported +
                response.Stats.RentPaymentsImported +
                response.Stats.DepositPaymentsImported +
                response.Stats.DepositReturnsImported +
                response.Stats.NotificationTemplatesImported +
                response.Stats.NotificationsImported +
                response.Stats.NotificationAttemptsImported;

            response.Message = $"Successfully imported {response.Stats.TotalItemsImported} items";
            response.Errors = errors;
            
            // Return ID mappings for reference
            response.IdMappings["houses"] = houseIdMap.ToDictionary(k => k.Key, v => v.Value);
            response.IdMappings["tenants"] = tenantIdMap.ToDictionary(k => k.Key, v => v.Value);
            response.IdMappings["tenancies"] = tenancyIdMap.ToDictionary(k => k.Key, v => v.Value);
            response.IdMappings["rentPayments"] = rentPaymentIdMap.ToDictionary(k => k.Key, v => v.Value);
            response.IdMappings["depositPayments"] = depositPaymentIdMap.ToDictionary(k => k.Key, v => v.Value);
            response.IdMappings["notificationTemplates"] = templateIdMap.ToDictionary(k => k.Key, v => v.Value);
            response.IdMappings["notifications"] = notificationIdMap.ToDictionary(k => k.Key, v => v.Value);

            _logger.LogInformation(
                "Bulk import completed: {Houses} houses, {Tenants} tenants, {Tenancies} tenancies, {Expenses} expenses, {Documents} documents, {RentPayments} rent payments, {DepositPayments} deposit payments, {DepositReturns} deposit returns, {NotificationTemplates} templates, {Notifications} notifications, {NotificationAttempts} attempts",
                response.Stats.HousesImported,
                response.Stats.TenantsImported,
                response.Stats.TenanciesImported,
                response.Stats.ExpensesImported,
                response.Stats.DocumentsImported,
                response.Stats.RentPaymentsImported,
                response.Stats.DepositPaymentsImported,
                response.Stats.DepositReturnsImported,
                response.Stats.NotificationTemplatesImported,
                response.Stats.NotificationsImported,
                response.Stats.NotificationAttemptsImported);

            return response;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Bulk import failed");
            
            response.Success = false;
            response.Message = $"Import failed: {ex.Message}";
            response.Errors = errors;
            response.Errors.Add($"Fatal error: {ex.Message}");
            
            return response;
        }
    }
}
