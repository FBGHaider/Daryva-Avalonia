using Daryva.Api.Data;
using Daryva.Api.Domain;
using Daryva.Api.Dtos;
using Daryva.Api.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Daryva.Api.Repositories;

public class NotificationRepository : INotificationRepository
{
    private readonly AppDbContext _dbContext;

    public NotificationRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<List<NotificationTemplate>> GetTemplatesAsync(string? channel, string? type, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.NotificationTemplates.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(channel))
            query = query.Where(t => t.Channel == channel);
        if (!string.IsNullOrWhiteSpace(type))
            query = query.Where(t => t.Type == type);

        return query
            .OrderByDescending(t => t.IsDefault)
            .ThenByDescending(t => t.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public Task<bool> AnyTemplatesForOrganizationAsync(Guid organizationId, CancellationToken cancellationToken = default)
        => _dbContext.NotificationTemplates
            .IgnoreQueryFilters()
            .AnyAsync(t => t.OrganizationId == organizationId, cancellationToken);

    public Task<NotificationTemplate?> GetTemplateByIdAsync(Guid templateId, CancellationToken cancellationToken = default)
        => _dbContext.NotificationTemplates.AsNoTracking().FirstOrDefaultAsync(t => t.Id == templateId, cancellationToken);

    public void AddTemplate(NotificationTemplate template) => _dbContext.NotificationTemplates.Add(template);

    public void UpdateTemplate(NotificationTemplate template) => _dbContext.NotificationTemplates.Update(template);

    public Task<List<Notification>> QueryNotificationsAsync(NotificationFilterRequest filter, CancellationToken cancellationToken = default)
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

        return query.OrderByDescending(n => n.ScheduledFor).ToListAsync(cancellationToken);
    }

    public Task<Notification?> GetNotificationByIdAsync(Guid notificationId, CancellationToken cancellationToken = default)
        => _dbContext.Notifications
            .Include(n => n.Tenant)
            .Include(n => n.Tenancy)
                .ThenInclude(t => t!.House)
            .Include(n => n.Attempts)
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == notificationId, cancellationToken);

    public Task<Notification?> GetTrackedNotificationByIdAsync(Guid notificationId, CancellationToken cancellationToken = default)
        => _dbContext.Notifications.FirstOrDefaultAsync(n => n.Id == notificationId, cancellationToken);

    public Task<List<Notification>> GetTrackedPendingDueAsync(DateTime asOf, CancellationToken cancellationToken = default)
        => _dbContext.Notifications
            .Where(n => n.Status == "Pending" && n.ScheduledFor <= asOf)
            .ToListAsync(cancellationToken);

    public void AddNotification(Notification notification) => _dbContext.Notifications.Add(notification);

    public void UpdateNotification(Notification notification) => _dbContext.Notifications.Update(notification);

    public void AddAttempt(NotificationAttempt attempt) => _dbContext.NotificationAttempts.Add(attempt);
}
