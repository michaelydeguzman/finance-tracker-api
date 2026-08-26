using System.ComponentModel.DataAnnotations;

namespace FinanceTracker.Domain.Entities
{
    /// <summary>
    /// Password material for users who have one. Absent for SSO-only accounts, which is
    /// why this is a separate 1:1 table rather than nullable columns on <see cref="User"/>.
    /// </summary>
    public class UserCredential
    {
        [Key]
        public Guid UserId { get; set; }
        public required User User { get; set; }

        /// <summary>
        /// Opaque, algorithm-tagged hash produced by ASP.NET Core's
        /// <c>PasswordHasher&lt;T&gt;</c>. Sized well past the current PBKDF2 output so a
        /// future rehash to a longer format does not need a migration.
        /// </summary>
        [Required]
        [MaxLength(500)]
        public string PasswordHash { get; set; } = string.Empty;

        /// <summary>
        /// Rotated on every password change or reset. Access tokens carry the stamp they
        /// were issued under, so bumping it invalidates every outstanding session —
        /// which is the whole point of "change password" after a suspected compromise.
        /// </summary>
        [Required]
        [MaxLength(64)]
        public string SecurityStamp { get; set; } = string.Empty;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
