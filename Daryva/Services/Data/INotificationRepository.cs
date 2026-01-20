using Daryva.MVVM.Models;

namespace Daryva.Services.Data
{
    public interface INotificationRepository
    {
        Task<IEnumerable<Notification>> GetAllNotificationsAsync(string? status = null, DateTime? startDate = null, DateTime? endDate = null, string? channel = null, string? type = null, int? tenantId = null, int? houseId = null);
        Task<IEnumerable<Notification>> GetPendingNotificationsAsync(DateTime? beforeDate = null);
        Task<Notification?> GetNotificationByIdAsync(int notificationId);
        Task<int> CreateNotificationAsync(Notification notification);
        Task UpdateNotificationAsync(Notification notification);
        Task DeleteNotificationAsync(int notificationId);
        Task<IEnumerable<NotificationTemplate>> GetAllTemplatesAsync(string? channel = null, string? type = null);
        Task<NotificationTemplate?> GetTemplateByIdAsync(int templateId);
        Task<int> CreateTemplateAsync(NotificationTemplate template);
        Task UpdateTemplateAsync(NotificationTemplate template);
        Task<int> CreateAttemptAsync(NotificationAttempt attempt);
        Task<IEnumerable<NotificationAttempt>> GetAttemptsByNotificationIdAsync(int notificationId);
    }
}
