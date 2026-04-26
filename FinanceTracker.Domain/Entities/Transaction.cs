using System.ComponentModel.DataAnnotations;

namespace FinanceTracker.Domain.Entities
{
    public class Transaction
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        [MaxLength(250)]
        public string Name { get; set; } = string.Empty;

        [Required]
        public required Guid CategoryId { get; set; }
        public required Category Category { get; set; }

        [MaxLength(500)]
        public string Description { get; set; } = string.Empty;

        public decimal Amount { get; set; }

        public DateTime TransactionDate { get; set; }

        // Optional Recurring configuration
        public Guid? FrequencyId { get; set; }

        // Navigation property
        public Frequency? Frequency { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public string CreatedBy { get; set; } = string.Empty;
    }
}
