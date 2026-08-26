using System.ComponentModel.DataAnnotations;

namespace FinanceTracker.Domain.Entities
{
    public enum RecurringTransactionStatus
    {
        Active,
        Paused,
        Cancelled
    }

    public class RecurringTransaction
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        [MaxLength(250)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        public decimal DefaultAmount { get; set; }

        [Required]
        public required Guid CategoryId { get; set; }
        public required Category Category { get; set; }

        /// <summary>
        /// Owning account, copied onto every <see cref="Transaction"/> this template
        /// generates so worker-created rows land in the right tenant.
        /// </summary>
        [Required]
        public required Guid UserId { get; set; }

        [Required]
        public required Guid FrequencyId { get; set; }
        public required Frequency Frequency { get; set; }

        public DateTime StartDate { get; set; }

        public DateTime? EndDate { get; set; }

        public DateTime NextOccurrenceDate { get; set; }

        [Required]
        public RecurringTransactionStatus Status { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public string CreatedBy { get; set; } = string.Empty;

        // Instances generated from this template
        public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
    }
}
