using FinanceTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Persistence.Configurations
{
    public class RecurringTransactionConfiguration : IEntityTypeConfiguration<RecurringTransaction>
    {
        public void Configure(EntityTypeBuilder<RecurringTransaction> builder)
        {
            builder.HasKey(e => e.Id);

            builder.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(250);

            builder.Property(e => e.Description)
                .HasMaxLength(500);

            builder.Property(e => e.DefaultAmount)
                .IsRequired()
                .HasPrecision(18, 2);

            builder.Property(e => e.StartDate)
                .IsRequired();

            builder.Property(e => e.NextOccurrenceDate)
                .IsRequired();

            builder.Property(e => e.Status)
                .IsRequired()
                .HasConversion<string>();

            builder.Property(e => e.CreatedAt)
                .IsRequired();

            builder.HasOne(e => e.Category)
                .WithMany(c => c.RecurringTransactions)
                .HasForeignKey(e => e.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(e => e.Frequency)
                .WithMany(f => f.RecurringTransactions)
                .HasForeignKey(e => e.FrequencyId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(e => e.Transactions)
                .WithOne(t => t.RecurringTransaction)
                .HasForeignKey(t => t.RecurringTransactionId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.HasOne<User>()
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(e => new { e.UserId, e.Status });
            builder.HasIndex(e => e.CategoryId);
            builder.HasIndex(e => e.FrequencyId);

            // The worker's sweep is cross-tenant and filters on Status + NextOccurrenceDate
            // with no UserId predicate, so it needs its own index that does not lead on the
            // tenant column.
            builder.HasIndex(e => new { e.Status, e.NextOccurrenceDate });
        }
    }
}
