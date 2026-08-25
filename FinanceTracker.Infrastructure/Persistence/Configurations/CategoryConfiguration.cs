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

            // Restrict on every tenancy FK, so deleting a user is an explicit, ordered
            // purge rather than a cascade that quietly takes financial records with it.
            builder.HasOne<User>()
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Tenant-leading: every category read is "this user's categories, of this type".
            builder.HasIndex(e => new { e.UserId, e.CategoryType });

            // A user's category names are unique to them; two users may both have "Rent".
            builder.HasIndex(e => new { e.UserId, e.Name })
                .IsUnique();
        }
    }
}