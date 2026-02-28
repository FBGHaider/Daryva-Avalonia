using Daryva.MVVM.Models;

namespace Daryva.Services.Business
{
    /// <summary>
    /// Service for current user notification preferences (Account page). Can be backed by local JSON or SaaS API later.
    /// </summary>
    public interface IUserPreferencesService
    {
        Task<UserNotificationPreferences> GetNotificationPreferencesAsync(CancellationToken cancellationToken = default);
        Task UpdateNotificationPreferencesAsync(UserNotificationPreferences prefs, CancellationToken cancellationToken = default);
    }
}
