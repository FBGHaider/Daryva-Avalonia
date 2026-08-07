using System.ComponentModel;
using System.Runtime.CompilerServices;
using Daryva.MVVM.Models;

namespace Daryva.MVVM.ViewModels
{
    /// <summary>
    /// ViewModel wrapper for a notification feed item (binding + IsRead updates).
    /// </summary>
    public class NotificationItemVm : INotifyPropertyChanged
    {
        private bool _isRead;

        public NotificationItemVm(NotificationItem model)
        {
            Id = model.Id;
            Type = model.Type;
            Title = model.Title;
            Message = model.Message;
            CreatedAt = model.CreatedAt;
            _isRead = model.IsRead;
            Severity = model.Severity;
            NavigationTarget = model.NavigationTarget;
        }

        public Guid Id { get; }
        public NotificationFeedType Type { get; }
        public string Title { get; }
        public string Message { get; }
        public DateTimeOffset CreatedAt { get; }
        public NotificationSeverity Severity { get; }
        public NotificationNavigationTarget? NavigationTarget { get; }

        /// <summary>Emoji icon for the notification type.</summary>
        public string TypeIcon => Type switch
        {
            NotificationFeedType.OverdueRent => "⚠",
            NotificationFeedType.RentDueSoon => "📅",
            NotificationFeedType.DocsExpiring => "📄",
            NotificationFeedType.PaymentReceived => "✓",
            NotificationFeedType.TeamActivity => "👥",
            NotificationFeedType.PortalSignupCompleted => "🔑",
            _ => "ℹ"
        };

        public bool IsRead
        {
            get => _isRead;
            set
            {
                if (_isRead == value) return;
                _isRead = value;
                OnPropertyChanged();
            }
        }

        /// <summary>Short relative time for display (e.g. "2h ago").</summary>
        public string CreatedAtDisplay => FormatRelativeTime(CreatedAt);

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private static string FormatRelativeTime(DateTimeOffset dt)
        {
            var span = DateTimeOffset.UtcNow - dt;
            if (span.TotalMinutes < 1) return "Just now";
            if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
            if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
            if (span.TotalDays < 7) return $"{(int)span.TotalDays}d ago";
            return dt.LocalDateTime.ToString("dd MMM");
        }
    }
}
