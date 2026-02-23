using System.ComponentModel.DataAnnotations;

namespace FinanceTracker.Domain.Entities
{
    public enum CategoryTypes
    {
        Income,
        Expense
    }

    public class Category
    {
        [Key]
        public Guid Id { get; set; }
        
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;
        
        [Required]
        public CategoryTypes CategoryType { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public bool IsActive { get; set; } = true;
        
        // Optional Recurring configuration
        
        public Guid? FrequencyId { get; set; }
        
        // Navigation property
        public Frequency? Frequency { get; set; }
    }
}
