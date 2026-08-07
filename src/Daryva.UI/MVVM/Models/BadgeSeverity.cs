namespace Daryva.MVVM.Models
{
    /// <summary>Drives StatusBadge's background/foreground pair. Status is never color-only --
    /// always paired with the badge's Text.</summary>
    public enum BadgeSeverity
    {
        Neutral,
        Success,
        Warning,
        Danger,
        Info,
        Purple
    }
}
