using Daryva.Services.Api;

namespace Daryva.MVVM.ViewModels
{
    /// <summary>Display-ready wrapper around a single SupportSessionDto row.</summary>
    public class SupportSessionVm
    {
        public Guid Id { get; init; }
        public Guid OrganizationId { get; init; }
        public string OrganizationName { get; init; } = string.Empty;
        public string Reason { get; init; } = string.Empty;
        public DateTime StartedAtUtc { get; init; }
        public DateTime ExpiresAtUtc { get; init; }
        public DateTime? EndedAtUtc { get; init; }
        public bool IsActive { get; init; }

        public string StartedAtDisplay => StartedAtUtc.ToLocalTime().ToString("dd MMM yyyy, HH:mm");
        public string ExpiresAtDisplay => ExpiresAtUtc.ToLocalTime().ToString("dd MMM yyyy, HH:mm");
        public string EndedAtDisplay => EndedAtUtc.HasValue ? EndedAtUtc.Value.ToLocalTime().ToString("dd MMM yyyy, HH:mm") : "-";

        public string StatusDisplay => IsActive ? "Active" : (EndedAtUtc.HasValue ? "Ended" : "Expired");

        public static SupportSessionVm FromDto(SupportSessionDto dto) => new()
        {
            Id = dto.Id,
            OrganizationId = dto.OrganizationId,
            OrganizationName = dto.OrganizationName,
            Reason = dto.Reason,
            StartedAtUtc = dto.StartedAt,
            ExpiresAtUtc = dto.ExpiresAt,
            EndedAtUtc = dto.EndedAt,
            IsActive = dto.IsActive
        };
    }
}
