using System.ComponentModel.DataAnnotations;

namespace FinanceTracker.Domain.Entities
{
    public enum FrequencyType
    {
        Daily,
        Weekly,
        BiWeekly,
        Monthly,
        Quarterly,
        SemiAnnually,
        Annually,
        Custom
    }

    public class Frequency
    {
        [Key]
        public Guid Id { get; set; }
        
        [Required]
        public string Name { get; set; } = string.Empty;
        
        [Required]
        public FrequencyType Type { get; set; }
        
        // For custom frequencies
        public int? IntervalDays { get; set; }
        
        public string? Description { get; set; }
        
        public bool IsActive { get; set; } = true;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        // Navigation property
        public ICollection<Category> Categories { get; set; } = new List<Category>();
    }
}