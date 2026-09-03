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

        /// <summary>Owning account. Categories are per-user, not shared reference data.</summary>
        [Required]
        public required Guid UserId { get; set; }
        
        /// <summary>
        /// The household this record is shared with, or null when its owner was on their own
        /// when it was written. Stamped from the writer's membership and rewritten in bulk
        /// when they join or leave, so the tenancy filter can widen with one scalar compare
        /// rather than a subquery over the membership table.
        /// </summary>
        public Guid? HouseholdId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public bool IsActive { get; set; } = true;

        public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
        public ICollection<RecurringTransaction> RecurringTransactions { get; set; } = new List<RecurringTransaction>();
    }
}
