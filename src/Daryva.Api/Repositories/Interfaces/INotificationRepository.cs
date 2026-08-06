using Daryva.Api.Domain;
using Daryva.Api.Dtos;

namespace Daryva.Api.Repositories.Interfaces;

/// <summary>
/// Covers NotificationTemplate, Notification, and NotificationAttempt -- kept as one repository
/// (rather than OrgScopedRepository&lt;T&gt; per entity) because RecordAttemptAsync must stage a
/// Notification update and a new NotificationAttempt together as one unit of work, the same way
/// IOrganizationRepository spans Organization/OrganizationInvite/OrganizationJoinCode.
/// </summary>
public interface INotificationRepository
{
    // Templates
    Task<List<NotificationTemplate>> GetTemplatesAsync(string? channel, string? type, CancellationToken cancellationToken = default);

    /// <summary>Ignores the org query filter -- used at seed time before request context has an org resolved.</summary>
    Task<bool> AnyTemplatesForOrganizationAsync(Guid organizationId, CancellationToken cancellationToken = default);

    Task<NotificationTemplate?> GetTemplateByIdAsync(Guid templateId, CancellationToken cancellationToken = default);

    void AddTemplate(NotificationTemplate template);

    void UpdateTemplate(NotificationTemplate template);

    // Notifications
    Task<List<Notification>> QueryNotificationsAsync(NotificationFilterRequest filter, CancellationToken cancellationToken = default);

    /// <summary>Includes Tenant, Tenancy.House, and Attempts -- the detail view shape.</summary>
    Task<Notification?> GetNotificationByIdAsync(Guid notificationId, CancellationToken cancellationToken = default);

    /// <summary>Tracked, no includes -- for send/cancel operations that only mutate the notification itself.</summary>
    Task<Notification?> GetTrackedNotificationByIdAsync(Guid notificationId, CancellationToken cancellationToken = default);

    /// <summary>Tracked list of Pending notifications due at or before asOf -- for the send queue processor.</summary>
    Task<List<Notification>> GetTrackedPendingDueAsync(DateTime asOf, CancellationToken cancellationToken = default);

    void AddNotification(Notification notification);

    void UpdateNotification(Notification notification);

    // Attempts
    void AddAttempt(NotificationAttempt attempt);
}
