using System.ComponentModel.DataAnnotations;

namespace FinanceTracker.Domain.Entities
{
    /// <summary>
    /// A group of people who share one set of financial records.
    ///
    /// A household never replaces <see cref="User"/> as the tenancy root — every record is
    /// still owned by the person who entered it. It is a second, wider scope layered on top:
    /// records created while their owner is in a household carry its id, and the tenancy
    /// filter admits a row on either match. That is what lets a member leave and take their
    /// own history with them rather than stranding it under a group they have left.
    /// </summary>
    public class Household
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// The member who may rename the household, invite, remove people, and delete it.
        /// Deliberately a single user rather than a role table: one household has one
        /// person answerable for who can see the money, and nothing here needs more.
        /// </summary>
        [Required]
        public required Guid OwnerUserId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<User> Members { get; set; } = new List<User>();

        public ICollection<HouseholdInvitation> Invitations { get; set; } = new List<HouseholdInvitation>();
    }
}
