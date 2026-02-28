using System.Text.Json;
using Daryva.MVVM.Models;
using Daryva.Services.Api;
using Daryva.Services.Platform;

namespace Daryva.Services.Business
{
    /// <summary>
    /// Persists user profile to local JSON in AppData. Seeds from auth API when missing.
    /// </summary>
    public class UserProfileService : IUserProfileService
    {
        private const string FileName = "user_profile.json";
        private readonly IAppPaths _appPaths;
        private readonly IAuthApiService _authApiService;

        public UserProfileService(IAppPaths appPaths, IAuthApiService authApiService)
        {
            _appPaths = appPaths ?? throw new ArgumentNullException(nameof(appPaths));
            _authApiService = authApiService ?? throw new ArgumentNullException(nameof(authApiService));
        }

        public async Task<UserProfile> GetProfileAsync(CancellationToken cancellationToken = default)
        {
            var path = Path.Combine(_appPaths.AppData, FileName);
            if (File.Exists(path))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
                    var stored = JsonSerializer.Deserialize<UserProfile>(json);
                    if (stored != null)
                    {
                        // Keep email in sync from auth when possible
                        var me = await _authApiService.GetMeAsync(cancellationToken).ConfigureAwait(false);
                        if (me != null && !string.IsNullOrEmpty(me.Email))
                            stored.Email = me.Email;
                        return stored;
                    }
                }
                catch
                {
                    // Fall through to seed from API
                }
            }

            var profile = await SeedFromAuthAsync(cancellationToken).ConfigureAwait(false);
            return profile;
        }

        public async Task UpdateProfileAsync(UserProfile profile, CancellationToken cancellationToken = default)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            var path = Path.Combine(_appPaths.AppData, FileName);
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(path, json, cancellationToken).ConfigureAwait(false);
        }

        private async Task<UserProfile> SeedFromAuthAsync(CancellationToken cancellationToken)
        {
            var profile = new UserProfile { TimeZoneId = "GMT Standard Time" };
            try
            {
                var me = await _authApiService.GetMeAsync(cancellationToken).ConfigureAwait(false);
                if (me != null)
                {
                    profile.Email = me.Email ?? string.Empty;
                    var first = (me.FirstName ?? "").Trim();
                    var last = (me.LastName ?? "").Trim();
                    profile.FullName = string.IsNullOrWhiteSpace(first) && string.IsNullOrWhiteSpace(last)
                        ? me.Email ?? "User"
                        : $"{first} {last}".Trim();
                }
            }
            catch
            {
                // Leave defaults
            }

            return profile;
        }
    }
}
