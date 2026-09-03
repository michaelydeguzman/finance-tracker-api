using FinanceTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinanceTracker.Infrastructure.Persistence.Configurations
{
    public class HouseholdInvitationConfiguration : IEntityTypeConfiguration<HouseholdInvitation>
    {
        public void Configure(EntityTypeBuilder<HouseholdInvitation> builder)
        {
            builder.HasKey(e => e.Id);

            builder.Property(e => e.InvitedEmail)
                .IsRequired()
                .HasMaxLength(320);

            builder.Property(e => e.Status)
                .IsRequired()
                .HasConversion<string>()
                .HasMaxLength(32);

            builder.Property(e => e.CreatedAt)
                .IsRequired();

            builder.Property(e => e.ExpiresAt)
                .IsRequired();

            // Cascade, unlike every other relationship here: an invitation is an offer, not
            // a financial record, and one whose household no longer exists is nothing but a
            // dead row somebody could still try to accept.
            builder.HasOne(e => e.Household)
                .WithMany(h => h.Invitations)
                .HasForeignKey(e => e.HouseholdId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne<User>()
                .WithMany()
                .HasForeignKey(e => e.InvitedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // "What is waiting for this address?" is the query the invitee's page runs on
            // every load, and the only one that is not already keyed by household.
            builder.HasIndex(e => new { e.InvitedEmail, e.Status });

            builder.HasIndex(e => new { e.HouseholdId, e.Status });
        }
    }
}
