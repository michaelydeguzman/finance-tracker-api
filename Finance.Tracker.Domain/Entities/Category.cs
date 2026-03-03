using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

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

        public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
    }
}
