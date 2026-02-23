using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using FinanceTracker.Domain.Entities;

namespace FinanceTracker.Infrastructure.Persistence.Configurations
{
    public class FrequencyConfiguration : IEntityTypeConfiguration<Frequency>
    {
        public void Configure(EntityTypeBuilder<Frequency> builder)
        {
            builder.HasKey(e => e.Id);

            builder.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(e => e.Type)
                .IsRequired()
                .HasConversion<string>();

            builder.Property(e => e.Description)
                .HasMaxLength(500);

            builder.Property(e => e.IntervalDays)
                .IsRequired(false);

            builder.Property(e => e.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            builder.Property(e => e.CreatedAt)
                .IsRequired();

            builder.HasIndex(e => e.Type);

            // Seed default frequencies with a fixed datetime
            var seedDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            
            builder.HasData(
                new Frequency
                {
                    Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                    Name = "Daily",
                    Type = FrequencyType.Daily,
                    IntervalDays = 1,
                    Description = "Occurs every day",
                    IsActive = true,
                    CreatedAt = seedDate
                },
                new Frequency
                {
                    Id = Guid.Parse("00000000-0000-0000-0000-000000000002"),
                    Name = "Weekly",
                    Type = FrequencyType.Weekly,
                    IntervalDays = 7,
                    Description = "Occurs every week",
                    IsActive = true,
                    CreatedAt = seedDate
                },
                new Frequency
                {
                    Id = Guid.Parse("00000000-0000-0000-0000-000000000003"),
                    Name = "Bi-Weekly",
                    Type = FrequencyType.BiWeekly,
                    IntervalDays = 14,
                    Description = "Occurs every two weeks",
                    IsActive = true,
                    CreatedAt = seedDate
                },
                new Frequency
                {
                    Id = Guid.Parse("00000000-0000-0000-0000-000000000004"),
                    Name = "Monthly",
                    Type = FrequencyType.Monthly,
                    IntervalDays = 30,
                    Description = "Occurs every month",
                    IsActive = true,
                    CreatedAt = seedDate
                },
                new Frequency
                {
                    Id = Guid.Parse("00000000-0000-0000-0000-000000000005"),
                    Name = "Quarterly",
                    Type = FrequencyType.Quarterly,
                    IntervalDays = 90,
                    Description = "Occurs every 3 months",
                    IsActive = true,
                    CreatedAt = seedDate
                },
                new Frequency
                {
                    Id = Guid.Parse("00000000-0000-0000-0000-000000000006"),
                    Name = "Semi-Annually",
                    Type = FrequencyType.SemiAnnually,
                    IntervalDays = 180,
                    Description = "Occurs every 6 months",
                    IsActive = true,
                    CreatedAt = seedDate
                },
                new Frequency
                {
                    Id = Guid.Parse("00000000-0000-0000-0000-000000000007"),
                    Name = "Annually",
                    Type = FrequencyType.Annually,
                    IntervalDays = 365,
                    Description = "Occurs every year",
                    IsActive = true,
                    CreatedAt = seedDate
                }
            );
        }
    }
}