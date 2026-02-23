using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using FinanceTracker.Domain.Entities;

namespace FinanceTracker.Infrastructure.Persistence.Configurations
{
    public class CategoryConfiguration : IEntityTypeConfiguration<Category>
    {
        public void Configure(EntityTypeBuilder<Category> builder)
        {
            builder.HasKey(e => e.Id);

            builder.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(e => e.CategoryType)
                .IsRequired()
                .HasConversion<string>();

            builder.Property(e => e.CreatedAt)
                .IsRequired();

            builder.Property(e => e.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            builder.HasIndex(e => e.CategoryType);

            builder.HasOne(e => e.Frequency)
                .WithMany(f => f.Categories)
                .HasForeignKey(e => e.FrequencyId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}