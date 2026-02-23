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
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;
        
        [Required]
        public FrequencyType Type { get; set; }
        
        // For custom frequencies
        public int? IntervalDays { get; set; }

        [MaxLength(250)]
        public string? Description { get; set; }
        
        public bool IsActive { get; set; } = true;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        // Navigation property
        public ICollection<Category> Categories { get; set; } = new List<Category>();
    }
}