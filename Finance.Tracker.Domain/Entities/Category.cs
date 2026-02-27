using System.ComponentModel.DataAnnotations;

namespace FinanceTracker.Domain.Entities
{
    public enum CategoryType
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
        public CategoryType CategoryType { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public bool IsActive { get; set; } = true;
        
        // Optional Recurring configuration
        
        public Guid? FrequencyId { get; set; }
        
        // Navigation property
        public Frequency? Frequency { get; set; }
    }
}
