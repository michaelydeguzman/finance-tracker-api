using FinanceTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Persistence.Configurations
{
    public class UserCredentialConfiguration : IEntityTypeConfiguration<UserCredential>
    {
        public void Configure(EntityTypeBuilder<UserCredential> builder)
        {
            builder.HasKey(e => e.UserId);

            builder.Property(e => e.PasswordHash)
                .IsRequired()
                .HasMaxLength(500);

            builder.Property(e => e.SecurityStamp)
                .IsRequired()
                .HasMaxLength(64);

            builder.Property(e => e.UpdatedAt)
                .IsRequired();
        }
    }
}
