using System.ComponentModel.DataAnnotations;

namespace FinanceTracker.Domain.Entities
{
    public enum UserStatus
    {
        Active,
        Disabled
    }

    /// <summary>
    /// A person who can sign in. Identity is deliberately split across three tables:
    /// <see cref="User"/> is the stable account (and the tenancy root every financial
    /// record hangs off), <see cref="UserIdentity"/> records each way that account can
    /// be authenticated, and <see cref="UserCredential"/> holds a password when one exists.
    /// That split is what lets one person use Google SSO *and* a password without
    /// ending up with two accounts and two disjoint sets of finances.
    /// </summary>
    public class User
    {
        [Key]
        public Guid Id { get; set; }

        /// <summary>
        /// Normalized to lowercase on write. Unique across all users, and the key used
        /// to decide whether an incoming SSO sign-in belongs to an existing account.
        /// </summary>
        [Required]
        [MaxLength(320)]
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// Null until the address is proven. Password sign-in and account linking both
        /// gate on this — an unverified address must never adopt an existing account.
        /// </summary>
        public DateTime? EmailVerifiedAt { get; set; }

        [MaxLength(250)]
        public string? DisplayName { get; set; }

        [Required]
        public UserStatus Status { get; set; } = UserStatus.Active;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<UserIdentity> Identities { get; set; } = new List<UserIdentity>();

        public UserCredential? Credential { get; set; }

        public ICollection<UserToken> Tokens { get; set; } = new List<UserToken>();
    }
}
