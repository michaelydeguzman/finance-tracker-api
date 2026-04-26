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

            builder.HasIndex(e => e.CategoryId);
            builder.HasIndex(e => e.TransactionDate);
            builder.HasIndex(e => e.CreatedAt);
        }
    }
}
