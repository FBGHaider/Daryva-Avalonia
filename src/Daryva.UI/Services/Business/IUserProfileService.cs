using Daryva.MVVM.Models;

namespace Daryva.Services.Business
{
    /// <summary>
    /// Service for current user profile (Account page). Can be backed by local JSON or SaaS API later.
    /// </summary>
    public interface IUserProfileService
    {
        Task<UserProfile> GetProfileAsync(CancellationToken cancellationToken = default);
        Task UpdateProfileAsync(UserProfile profile, CancellationToken cancellationToken = default);
    }
}
