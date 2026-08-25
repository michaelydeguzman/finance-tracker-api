using System.ComponentModel.DataAnnotations;

namespace FinanceTracker.Domain.Entities
{
    public enum UserTokenPurpose
    {
        EmailVerification,
        PasswordReset,
        MagicLink
    }

    /// <summary>
    /// A single-use, expiring secret emailed to a user. All three flows share this table
    /// so that expiry, single-use consumption, and hashing are implemented once rather
    /// than three times with three chances to get them wrong.
    /// </summary>
    public class UserToken
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public required Guid UserId { get; set; }
        public required User User { get; set; }

        [Required]
        public UserTokenPurpose Purpose { get; set; }

        /// <summary>
        /// SHA-256 of the token that was emailed, never the token itself. A leaked
        /// database backup must not hand over working password-reset links.
        /// </summary>
        [Required]
        [MaxLength(128)]
        public string TokenHash { get; set; } = string.Empty;

        public DateTime ExpiresAt { get; set; }

        /// <summary>Set the moment the token is redeemed; a non-null value means spent.</summary>
        public DateTime? ConsumedAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
