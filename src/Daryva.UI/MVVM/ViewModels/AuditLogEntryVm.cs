using System.Text.RegularExpressions;

namespace Daryva.MVVM.ViewModels
{
    /// <summary>Display-ready wrapper around a single AuditLogEntryDto row.</summary>
    public class AuditLogEntryVm
    {
        public Guid Id { get; init; }
        public DateTime CreatedAtUtc { get; init; }
        public string EventType { get; init; } = string.Empty;
        public string ActorRole { get; init; } = string.Empty;
        public Guid ActorUserId { get; init; }
        public string? TargetType { get; init; }
        public string? TargetId { get; init; }
        public string? IpAddress { get; init; }
        public string? Metadata { get; init; }

        public string CreatedAtDisplay => CreatedAtUtc.ToLocalTime().ToString("dd MMM yyyy, HH:mm:ss");

        /// <summary>"AuthLoginFailed" -> "Auth Login Failed" -- the raw constants read fine in code, not in a table.</summary>
        public string EventTypeDisplay => Regex.Replace(EventType, "(?<!^)([A-Z])", " $1");

        public string ActorDisplay => ActorUserId == Guid.Empty
            ? ActorRole
            : $"{ActorRole} ({ActorUserId.ToString()[..8]})";

        public string TargetDisplay => string.IsNullOrWhiteSpace(TargetType)
            ? "-"
            : string.IsNullOrWhiteSpace(TargetId) ? TargetType : $"{TargetType} ({TargetId})";

        public string IpAddressDisplay => string.IsNullOrWhiteSpace(IpAddress) ? "-" : IpAddress;

        public bool HasMetadata => !string.IsNullOrWhiteSpace(Metadata) && Metadata != "{}";
    }
}
