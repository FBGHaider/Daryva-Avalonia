using Daryva.Api.Data;
using Daryva.Api.Domain;
using Daryva.Api.Dtos;
using Daryva.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Daryva.Api.Services;

public class NotificationService : INotificationService
{
    private readonly AppDbContext _dbContext;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(AppDbContext dbContext, IEmailSender emailSender, ILogger<NotificationService> logger)
    {
        _dbContext = dbContext;
        _emailSender = emailSender;
        _logger = logger;
    }

    public async Task<List<NotificationRecipientResponse>> BuildRecipientsAsync(RecipientFilterRequest filter, CancellationToken cancellationToken = default)
    {
        var recipients = new List<NotificationRecipientResponse>();

        var tenantQuery = _dbContext.Tenants.AsNoTracking().Where(t => !t.IsArchived);

        if (string.Equals(filter.TargetType, "Single", StringComparison.OrdinalIgnoreCase) && filter.TenantId.HasValue)
        {
            tenantQuery = tenantQuery.Where(t => t.Id == filter.TenantId.Value);
        }

        if (string.Equals(filter.TargetType, "House", StringComparison.OrdinalIgnoreCase) && filter.HouseId.HasValue)
        {
            var houseTenancies = await _dbContext.Tenancies
                .AsNoTracking()
                .Where(t => t.HouseId == filter.HouseId.Value && t.Status == "Active")
                .ToListAsync(cancellationToken);

            var tenantIds = houseTenancies.Select(t => t.TenantId).Distinct().ToList();
            tenantQuery = tenantQuery.Where(t => tenantIds.Contains(t.Id));
        }

        var tenants = await tenantQuery.ToListAsync(cancellationToken);
        foreach (var tenant in tenants)
        {
            var tenancy = await _dbContext.Tenancies
                .AsNoTracking()
                .Include(t => t.House)
                .Where(t => t.TenantId == tenant.Id && t.Status == "Active")
                .OrderByDescending(t => t.MoveInDate)
                .FirstOrDefaultAsync(cancellationToken);

            recipients.Add(new NotificationRecipientResponse
            {
                TenantId = tenant.Id,
                TenantName = tenant.FullName,
                Email = tenant.Email,
                PhoneNumber = tenant.PhoneNumber,
                TenancyId = tenancy?.Id,
                HouseAddressLine1 = tenancy?.House?.AddressLine1 ?? "Unknown",
                HouseCity = tenancy?.House?.City ?? "",
                HasEmail = !string.IsNullOrWhiteSpace(tenant.Email),
                HasWhatsApp = !string.IsNullOrWhiteSpace(tenant.PhoneNumber),
                AmountDue = null,
                DueDate = null
            });
        }

        return recipients;
    }

    public async Task<List<NotificationTemplate>> GetTemplatesAsync(string? channel, string? type, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.NotificationTemplates.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(channel))
            query = query.Where(t => t.Channel == channel);
        if (!string.IsNullOrWhiteSpace(type))
            query = query.Where(t => t.Type == type);

        return await query
            .OrderByDescending(t => t.IsDefault)
            .ThenByDescending(t => t.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task SeedDefaultTemplatesAsync(Guid organizationId, CancellationToken cancellationToken = default)
    {
        var anyExists = await _dbContext.NotificationTemplates
            .IgnoreQueryFilters()
            .AnyAsync(t => t.OrganizationId == organizationId, cancellationToken);
        if (anyExists)
            return;

        var defaults = new[]
        {
            new NotificationTemplate
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                Name = "Rent Due Reminder",
                Channel = "Email",
                Type = "RentDue",
                SubjectTemplate = "Rent Due Reminder - {Month}",
                BodyTemplate = "Dear {TenantName},\r\n\r\nThis is a reminder that your rent payment of £{AmountDue} for {Month} is due on {DueDate}.\r\n\r\nProperty: {HouseAddress}\r\n\r\nPlease ensure payment is made by the due date.\r\n\r\nPayment Instructions: {PayInstructions}\r\n\r\nThank you.",
                IsDefault = true,
                CreatedAt = DateTime.UtcNow
            },
            new NotificationTemplate
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                Name = "Rent Overdue",
                Channel = "Email",
                Type = "RentOverdue",
                SubjectTemplate = "URGENT: Rent Overdue - {Month}",
                BodyTemplate = "Dear {TenantName},\r\n\r\nThis is to inform you that your rent payment of £{AmountDue} for {Month} is now overdue.\r\n\r\nProperty: {HouseAddress}\r\nDue Date: {DueDate}\r\n\r\nPlease arrange payment immediately to avoid further action.\r\n\r\nPayment Instructions: {PayInstructions}\r\n\r\nThank you.",
                IsDefault = true,
                CreatedAt = DateTime.UtcNow
            },
            new NotificationTemplate
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                Name = "Missing Student Letter",
                Channel = "Email",
                Type = "MissingDocuments",
                SubjectTemplate = "Missing Student Confirmation Letter",
                BodyTemplate = "Dear {TenantName},\r\n\r\nThis is a reminder that we are still missing your Student Confirmation Letter.\r\n\r\nProperty: {HouseAddress}\r\n\r\nPlease provide this document at your earliest convenience.\r\n\r\nThank you.",
                IsDefault = true,
                CreatedAt = DateTime.UtcNow
            },
            new NotificationTemplate
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                Name = "General Message",
                Channel = "Email",
                Type = "General",
                SubjectTemplate = "Message from Landlord",
                BodyTemplate = "Dear {TenantName},\r\n\r\n{Message}\r\n\r\nProperty: {HouseAddress}\r\n\r\nThank you.",
                IsDefault = true,
                CreatedAt = DateTime.UtcNow
            }
        };

        foreach (var t in defaults)
            _dbContext.NotificationTemplates.Add(t);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<NotificationTemplate?> GetTemplateByIdAsync(Guid templateId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.NotificationTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == templateId, cancellationToken);
    }

    public async Task<NotificationTemplate> CreateTemplateAsync(NotificationTemplate template, CancellationToken cancellationToken = default)
    {
        _dbContext.NotificationTemplates.Add(template);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return template;
    }

    public async Task UpdateTemplateAsync(NotificationTemplate template, CancellationToken cancellationToken = default)
    {
        _dbContext.NotificationTemplates.Update(template);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<Notification>> GetNotificationsAsync(NotificationFilterRequest filter, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Notifications
            .Include(n => n.Tenant)
            .Include(n => n.Tenancy)
                .ThenInclude(t => t!.House)
            .Include(n => n.Attempts)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.Status))
            query = query.Where(n => n.Status == filter.Status);
        if (filter.StartDate.HasValue)
            query = query.Where(n => n.ScheduledFor >= filter.StartDate.Value);
        if (filter.EndDate.HasValue)
            query = query.Where(n => n.ScheduledFor <= filter.EndDate.Value);
        if (!string.IsNullOrWhiteSpace(filter.Channel))
            query = query.Where(n => n.Channel == filter.Channel);
        if (!string.IsNullOrWhiteSpace(filter.Type))
            query = query.Where(n => n.Type == filter.Type);
        if (filter.TenantId.HasValue)
            query = query.Where(n => n.TenantId == filter.TenantId.Value);
        if (filter.HouseId.HasValue)
            query = query.Where(n => n.Tenancy != null && n.Tenancy.HouseId == filter.HouseId.Value);

        return await query
            .OrderByDescending(n => n.ScheduledFor)
            .ToListAsync(cancellationToken);
    }

    public async Task<Notification?> GetNotificationByIdAsync(Guid notificationId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Notifications
            .Include(n => n.Tenant)
            .Include(n => n.Tenancy)
                .ThenInclude(t => t!.House)
            .Include(n => n.Attempts)
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == notificationId, cancellationToken);
    }

    public async Task<Notification> CreateNotificationAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        _dbContext.Notifications.Add(notification);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return notification;
    }

    public async Task UpdateNotificationAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        _dbContext.Notifications.Update(notification);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task CancelNotificationAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        notification.Status = "Cancelled";
        _dbContext.Notifications.Update(notification);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> SendNotificationAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        if (notification.Status != "Pending")
            return false;

        try
        {
            bool success = false;
            string? error = null;
            string? providerMessageId = null;

            if (string.Equals(notification.Channel, "Email", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(notification.ToAddress))
                {
                    error = "Missing recipient email address.";
                }
                else
                {
                    success = await _emailSender.SendEmailAsync(notification.ToAddress, notification.Subject ?? "", notification.Body);
                }
            }
            else if (string.Equals(notification.Channel, "SMS", StringComparison.OrdinalIgnoreCase))
            {
                error = "SMS integration is not configured.";
            }
            else if (string.Equals(notification.Channel, "WhatsApp", StringComparison.OrdinalIgnoreCase))
            {
                error = "WhatsApp integration is not configured.";
            }
            else
            {
                error = "Unknown notification channel.";
            }

            await RecordAttemptAsync(notification, success, error, providerMessageId, cancellationToken);
            return success;
        }
        catch (Exception ex)
        {
            await RecordAttemptAsync(notification, false, ex.Message, null, cancellationToken);
            return false;
        }
    }

    public async Task<bool> SendNotificationWithContentAsync(Notification notification, string toAddress, string subject, string body, CancellationToken cancellationToken = default)
    {
        if (notification.Status != "Pending")
            return false;

        if (string.IsNullOrWhiteSpace(toAddress))
        {
            await RecordAttemptAsync(notification, false, "Missing recipient email address.", null, cancellationToken);
            return false;
        }

        try
        {
            var success = await _emailSender.SendEmailAsync(toAddress, subject, body);
            notification.ToAddress = toAddress;
            notification.Subject = subject;
            notification.Body = body;
            await RecordAttemptAsync(notification, success, success ? null : "Send returned false.", null, cancellationToken);
            return success;
        }
        catch (Exception ex)
        {
            await RecordAttemptAsync(notification, false, ex.Message, null, cancellationToken);
            return false;
        }
    }

    public async Task<bool> SendBatchAsync(IEnumerable<Guid> notificationIds, CancellationToken cancellationToken = default)
    {
        var results = new List<bool>();
        foreach (var id in notificationIds)
        {
            var notification = await _dbContext.Notifications.FirstOrDefaultAsync(n => n.Id == id, cancellationToken);
            if (notification == null)
                continue;
            results.Add(await SendNotificationAsync(notification, cancellationToken));
        }
        return results.All(r => r);
    }

    public async Task<int> ProcessDueQueueAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var pending = await _dbContext.Notifications
            .Where(n => n.Status == "Pending" && n.ScheduledFor <= now)
            .ToListAsync(cancellationToken);

        var sent = 0;
        foreach (var notification in pending)
        {
            if (await SendNotificationAsync(notification, cancellationToken))
                sent++;
        }
        return sent;
    }

    private async Task RecordAttemptAsync(Notification notification, bool success, string? error, string? providerMessageId, CancellationToken cancellationToken)
    {
        var attempt = new NotificationAttempt
        {
            Id = Guid.NewGuid(),
            OrganizationId = notification.OrganizationId,
            NotificationId = notification.Id,
            AttemptedAt = DateTime.UtcNow,
            Status = success ? "Success" : "Failed",
            Error = error,
            ProviderMessageId = providerMessageId
        };

        _dbContext.NotificationAttempts.Add(attempt);

        notification.Status = success ? "Sent" : "Failed";
        notification.SentAt = DateTime.UtcNow;
        notification.Error = error;
        notification.ProviderMessageId = providerMessageId;
        _dbContext.Notifications.Update(notification);

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
