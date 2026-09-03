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

        /// <summary>Owning account. Every tenancy-scoped query filters on this.</summary>
        [Required]
        public required Guid UserId { get; set; }

        /// <summary>
        /// The household this record is shared with, or null when its owner was on their own
        /// when it was written. Stamped from the writer's membership and rewritten in bulk
        /// when they join or leave, so the tenancy filter can widen with one scalar compare
        /// rather than a subquery over the membership table.
        /// </summary>
        public Guid? HouseholdId { get; set; }

        [MaxLength(500)]
        public string Description { get; set; } = string.Empty;

        public decimal Amount { get; set; }

        public DateTime TransactionDate { get; set; }

        public Guid? RecurringTransactionId { get; set; }
        public RecurringTransaction? RecurringTransaction { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public string CreatedBy { get; set; } = string.Empty;
    }
}
