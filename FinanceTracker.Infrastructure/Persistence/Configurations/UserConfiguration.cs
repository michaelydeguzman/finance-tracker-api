using FinanceTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Persistence.Configurations
{
    public class UserConfiguration : IEntityTypeConfiguration<User>
    {
        public void Configure(EntityTypeBuilder<User> builder)
        {
            builder.HasKey(e => e.Id);

            builder.Property(e => e.Email)
                .IsRequired()
                .HasMaxLength(320);

            builder.Property(e => e.DisplayName)
                .HasMaxLength(250);

            builder.Property(e => e.Status)
                .IsRequired()
                .HasConversion<string>();

            builder.Property(e => e.CreatedAt)
                .IsRequired();

            // Emails are normalized to lowercase before write, so a plain unique index is
            // enough to stop two accounts claiming the same address under different casing.
            builder.HasIndex(e => e.Email)
                .IsUnique();

            builder.HasMany(e => e.Identities)
                .WithOne(i => i.User)
                .HasForeignKey(i => i.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(e => e.Credential)
                .WithOne(c => c.User)
                .HasForeignKey<UserCredential>(c => c.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(e => e.Tokens)
                .WithOne(t => t.User)
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
