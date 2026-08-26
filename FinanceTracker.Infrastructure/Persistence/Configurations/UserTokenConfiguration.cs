using FinanceTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Persistence.Configurations
{
    public class UserTokenConfiguration : IEntityTypeConfiguration<UserToken>
    {
        public void Configure(EntityTypeBuilder<UserToken> builder)
        {
            builder.HasKey(e => e.Id);

            builder.Property(e => e.Purpose)
                .IsRequired()
                .HasConversion<string>()
                .HasMaxLength(32);

            builder.Property(e => e.TokenHash)
                .IsRequired()
                .HasMaxLength(128);

            builder.Property(e => e.ExpiresAt)
                .IsRequired();

            builder.Property(e => e.CreatedAt)
                .IsRequired();

            // Redemption looks a token up by hash alone — the URL carries no user id.
            builder.HasIndex(e => e.TokenHash)
                .IsUnique();

            // Supports "invalidate this user's outstanding reset tokens" on issue.
            builder.HasIndex(e => new { e.UserId, e.Purpose });
        }
    }
}
