namespace FBGRentora.MVVM.Models
{
    public class NotificationAttempt
    {
        public int AttemptId { get; set; }
        public int NotificationId { get; set; }
        public DateTime AttemptedAt { get; set; }
        public string Status { get; set; } = string.Empty; // Success, Failed
        public string? Error { get; set; }
        public string? ProviderMessageId { get; set; }
        
        // Navigation
        public Notification? Notification { get; set; }
    }
}
