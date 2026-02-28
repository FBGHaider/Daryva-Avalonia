using System.Text.Json;
using Daryva.MVVM.Models;
using Daryva.Services.Platform;

namespace Daryva.Services.Business
{
    /// <summary>
    /// Persists user notification preferences to local JSON in AppData.
    /// </summary>
    public class UserPreferencesService : IUserPreferencesService
    {
        private const string FileName = "user_notification_preferences.json";
        private readonly IAppPaths _appPaths;

        public UserPreferencesService(IAppPaths appPaths)
        {
            _appPaths = appPaths ?? throw new ArgumentNullException(nameof(appPaths));
        }

        public async Task<UserNotificationPreferences> GetNotificationPreferencesAsync(CancellationToken cancellationToken = default)
        {
            var path = Path.Combine(_appPaths.AppData, FileName);
            if (File.Exists(path))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
                    var stored = JsonSerializer.Deserialize<UserNotificationPreferences>(json);
                    if (stored != null)
                        return stored;
                }
                catch
                {
                    // Fall through to defaults
                }
            }

            return new UserNotificationPreferences();
        }

        public async Task UpdateNotificationPreferencesAsync(UserNotificationPreferences prefs, CancellationToken cancellationToken = default)
        {
            if (prefs == null) throw new ArgumentNullException(nameof(prefs));
            var path = Path.Combine(_appPaths.AppData, FileName);
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(prefs, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(path, json, cancellationToken).ConfigureAwait(false);
        }
    }
}
