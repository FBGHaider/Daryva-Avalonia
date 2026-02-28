namespace Daryva.MVVM.Models
{
    /// <summary>
    /// Organisation for local/SaaS org management.
    /// </summary>
    public class Organisation
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string? PlanTier { get; set; }
    }
}
