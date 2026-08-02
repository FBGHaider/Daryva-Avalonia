using Daryva.Api.Domain;
using Daryva.Api.Dtos;

namespace Daryva.Api.Services.Interfaces;

public interface INotificationService
{
    Task<List<NotificationRecipientResponse>> BuildRecipientsAsync(RecipientFilterRequest filter, CancellationToken cancellationToken = default);
    Task<List<NotificationTemplate>> GetTemplatesAsync(string? channel, string? type, CancellationToken cancellationToken = default);
    Task SeedDefaultTemplatesAsync(Guid organizationId, CancellationToken cancellationToken = default);
    Task<NotificationTemplate?> GetTemplateByIdAsync(Guid templateId, CancellationToken cancellationToken = default);
    Task<NotificationTemplate> CreateTemplateAsync(NotificationTemplate template, CancellationToken cancellationToken = default);
    Task UpdateTemplateAsync(NotificationTemplate template, CancellationToken cancellationToken = default);
    Task<List<Notification>> GetNotificationsAsync(NotificationFilterRequest filter, CancellationToken cancellationToken = default);
    Task<Notification?> GetNotificationByIdAsync(Guid notificationId, CancellationToken cancellationToken = default);
    Task<Notification> CreateNotificationAsync(Notification notification, CancellationToken cancellationToken = default);
    Task UpdateNotificationAsync(Notification notification, CancellationToken cancellationToken = default);
    Task CancelNotificationAsync(Notification notification, CancellationToken cancellationToken = default);
    Task<bool> SendNotificationAsync(Notification notification, CancellationToken cancellationToken = default);
    Task<bool> SendNotificationWithContentAsync(Notification notification, string toAddress, string subject, string body, CancellationToken cancellationToken = default);
    Task<bool> SendBatchAsync(IEnumerable<Guid> notificationIds, CancellationToken cancellationToken = default);
    Task<int> ProcessDueQueueAsync(CancellationToken cancellationToken = default);
}
