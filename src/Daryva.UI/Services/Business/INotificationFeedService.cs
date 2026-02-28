using Daryva.MVVM.Models;

namespace Daryva.Services.Business
{
    /// <summary>
    /// Service for the header notification feed (overdue rent, rent due soon, docs expiring, payments, etc.).
    /// </summary>
    public interface INotificationFeedService
    {
        /// <summary>
        /// Gets all notifications for the current user/org.
        /// </summary>
        Task<IReadOnlyList<NotificationItem>> GetNotificationsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Marks a single notification as read.
        /// </summary>
        Task MarkAsReadAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Marks all notifications as read (optionally for the given IDs).
        /// </summary>
        Task MarkAllAsReadAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Marks the given notification IDs as read.
        /// </summary>
        Task MarkAllAsReadAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);
    }
}
