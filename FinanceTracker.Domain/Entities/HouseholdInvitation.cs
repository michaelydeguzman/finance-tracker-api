using System.ComponentModel.DataAnnotations;

namespace FinanceTracker.Domain.Entities
{
    public enum HouseholdInvitationStatus
    {
        Pending,
        Accepted,
        Declined,

        /// <summary>Withdrawn by the household owner before it was answered.</summary>
        Revoked
    }

    /// <summary>
    /// An offer to join a household, addressed to an email rather than to a user id.
    ///
    /// Addressed by email on purpose, and answered only by the person signed in as that
    /// address: joining a household exposes the joiner's own records to everyone already in
    /// it, so it cannot be something another user does to them. An invitation to an address
    /// with no account yet is still valid — it is waiting for whoever registers it and
    /// proves the address.
    /// </summary>
    public class HouseholdInvitation
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public required Guid HouseholdId { get; set; }
        public Household? Household { get; set; }

        /// <summary>Normalized to lowercase on write, like <see cref="User.Email"/>.</summary>
        [Required]
        [MaxLength(320)]
        public required string InvitedEmail { get; set; }

        [Required]
        public required Guid InvitedByUserId { get; set; }

        [Required]
        public HouseholdInvitationStatus Status { get; set; } = HouseholdInvitationStatus.Pending;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime ExpiresAt { get; set; }

        /// <summary>Set the moment the invitation is accepted, declined or revoked.</summary>
        public DateTime? RespondedAt { get; set; }

        public bool IsOpen(DateTime asOf) =>
            Status == HouseholdInvitationStatus.Pending && ExpiresAt > asOf;
    }
}
