using FinanceTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Persistence.Configurations
{
    public class UserIdentityConfiguration : IEntityTypeConfiguration<UserIdentity>
    {
        public void Configure(EntityTypeBuilder<UserIdentity> builder)
        {
            builder.HasKey(e => e.Id);

            builder.Property(e => e.Provider)
                .IsRequired()
                .HasConversion<string>()
                .HasMaxLength(32);

            builder.Property(e => e.ProviderSubject)
                .IsRequired()
                .HasMaxLength(250);

            builder.Property(e => e.CreatedAt)
                .IsRequired();

            // The sign-in lookup, and the constraint that stops one Google account being
            // attached to two users.
            builder.HasIndex(e => new { e.Provider, e.ProviderSubject })
                .IsUnique();

            // At most one identity per provider per user: a second Google identity on the
            // same account would be a linking bug, not a legitimate state.
            builder.HasIndex(e => new { e.UserId, e.Provider })
                .IsUnique();
        }
    }
}
