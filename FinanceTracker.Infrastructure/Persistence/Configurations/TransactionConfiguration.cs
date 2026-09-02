using FinanceTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Persistence.Configurations
{
    public class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
    {
        public void Configure(EntityTypeBuilder<Transaction> builder)
        {
            builder.HasKey(e => e.Id);

            builder.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(250);

            builder.Property(e => e.Description)
                .HasMaxLength(500);

            builder.Property(e => e.Amount)
                .IsRequired()
                .HasPrecision(18, 2);

            builder.Property(e => e.TransactionDate)
                .IsRequired();

            builder.HasOne(e => e.Category)
                .WithMany(category => category.Transactions)
                .HasForeignKey(e => e.CategoryId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Property(e => e.CreatedAt)
                .IsRequired();

            // Restrict, not Cascade: deleting a user must be a deliberate, explicit
            // purge of their financial records, never a silent side effect.
            builder.HasOne<User>()
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Tenant-leading composites replace the old single-column indexes: with a
            // UserId filter on every query, a bare TransactionDate index is unusable.

            // Restrict here as well. A household is deleted only once it is empty, and a
            // cascade would turn "the owner deleted the household" into "everyone's shared
            // history disappeared".
            builder.HasOne<Household>()
                .WithMany()
                .HasForeignKey(e => e.HouseholdId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(e => new { e.UserId, e.TransactionDate });
            builder.HasIndex(e => new { e.UserId, e.CategoryId });
            builder.HasIndex(e => new { e.UserId, e.CreatedAt });

            // Household-leading counterparts, for the same reason the tenant-leading ones
            // exist: the shared view sorts and pages by date exactly as the private one does.
            builder.HasIndex(e => new { e.HouseholdId, e.TransactionDate });
            builder.HasIndex(e => new { e.HouseholdId, e.CreatedAt });
        }
    }
}
