using System.ComponentModel.DataAnnotations;

namespace FinanceTracker.Domain.Entities
{
    public enum IdentityProvider
    {
        Password,
        Google,
        GitHub
    }

    /// <summary>
    /// One way a <see cref="User"/> can authenticate. A user may hold several — a Google
    /// identity and a password identity both pointing at the same account.
    /// </summary>
    public class UserIdentity
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public required Guid UserId { get; set; }
        public required User User { get; set; }

        [Required]
        public IdentityProvider Provider { get; set; }

        /// <summary>
        /// The provider's stable identifier for this person: the OIDC <c>sub</c> for
        /// Google, the account id for GitHub, and the owning <see cref="User.Id"/> for
        /// password identities. Deliberately not the email address — emails change, and
        /// a mutable natural key here would silently re-point an identity at sign-in.
        /// </summary>
        [Required]
        [MaxLength(250)]
        public string ProviderSubject { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
