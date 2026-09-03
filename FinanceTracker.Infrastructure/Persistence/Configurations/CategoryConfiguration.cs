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


            // Restrict here as well. A household is deleted only once it is empty, and a
            // cascade would turn "the owner deleted the household" into "everyone's shared
            // history disappeared".
            builder.HasOne<Household>()
                .WithMany()
                .HasForeignKey(e => e.HouseholdId)
                .OnDelete(DeleteBehavior.Restrict);

            // Tenant-leading: every category read is "this user's categories, of this type".
            builder.HasIndex(e => new { e.UserId, e.CategoryType });

            // The other half of the tenancy filter. A household read is "everything stamped
            // with this household, of this type", and without its own index it degrades to a
            // scan the moment a household has any history.
            builder.HasIndex(e => new { e.HouseholdId, e.CategoryType });

            // Unique per user *and per type*: "Other" as an income category and "Other" as
            // an expense category are distinct things, and the app already presents the two
            // lists separately. Scoping uniqueness to the name alone would reject that.
            builder.HasIndex(e => new { e.UserId, e.CategoryType, e.Name })
                .IsUnique();
        }
    }
}