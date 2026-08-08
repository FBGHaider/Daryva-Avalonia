using Daryva.Services.Api;

namespace Daryva.MVVM.ViewModels
{
    /// <summary>Display-ready wrapper around a single PlatformAdminDto row.</summary>
    public class PlatformAdminVm
    {
        public Guid Id { get; init; }
        public string Email { get; init; } = string.Empty;
        public string FirstName { get; init; } = string.Empty;
        public string LastName { get; init; } = string.Empty;
        public DateTime CreatedAt { get; init; }
        public bool IsSelf { get; init; }

        public string DisplayName => string.IsNullOrWhiteSpace(FirstName) && string.IsNullOrWhiteSpace(LastName)
            ? Email
            : $"{FirstName} {LastName}".Trim();

        public string CreatedAtDisplay => CreatedAt.ToLocalTime().ToString("dd MMM yyyy");

        /// <summary>Client-side mirror of the server's own self-revoke block -- disables the button
        /// rather than relying solely on the 400 the server would return.</summary>
        public bool CanRevoke => !IsSelf;

        public static PlatformAdminVm FromDto(PlatformAdminDto dto, string? selfUserId) => new()
        {
            Id = dto.Id,
            Email = dto.Email,
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            CreatedAt = dto.CreatedAt,
            IsSelf = Guid.TryParse(selfUserId, out var selfGuid) && selfGuid == dto.Id
        };
    }
}
