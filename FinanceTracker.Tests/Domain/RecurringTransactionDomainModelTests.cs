using FinanceTracker.Domain.Entities;
using FinanceTracker.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Tests.Domain;

public class RecurringTransactionDomainModelTests
{
    [Fact]
    public void RecurringTransaction_EntityConstruction_AllFieldsSet()
    {
        var id = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var frequencyId = Guid.NewGuid();
        var startDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var nextOccurrenceDate = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        var createdAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var category = new Category { Id = categoryId, Name = "Bills", CategoryType = CategoryType.Expense, UserId = TestCurrentUserAccessor.DefaultUserId };
        var frequency = new Frequency { Id = frequencyId, Name = "Monthly", Type = FrequencyType.Monthly };

        var entity = new RecurringTransaction
        {
            Id = id,
            Name = "Rent",
            Description = "Monthly rent payment",
            DefaultAmount = 1200.00m,
            CategoryId = categoryId,
            Category = category,
            UserId = TestCurrentUserAccessor.DefaultUserId,
            FrequencyId = frequencyId,
            Frequency = frequency,
            StartDate = startDate,
            EndDate = null,
            NextOccurrenceDate = nextOccurrenceDate,
            Status = RecurringTransactionStatus.Active,
            CreatedAt = createdAt,
            CreatedBy = "test-user"
        };

        entity.Id.Should().Be(id);
        entity.Name.Should().Be("Rent");
        entity.Description.Should().Be("Monthly rent payment");
        entity.DefaultAmount.Should().Be(1200.00m);
        entity.CategoryId.Should().Be(categoryId);
        entity.FrequencyId.Should().Be(frequencyId);
        entity.StartDate.Should().Be(startDate);
        entity.EndDate.Should().BeNull();
        entity.NextOccurrenceDate.Should().Be(nextOccurrenceDate);
        entity.Status.Should().Be(RecurringTransactionStatus.Active);
        entity.CreatedAt.Should().Be(createdAt);
        entity.CreatedBy.Should().Be("test-user");
    }

    [Fact]
    public void RecurringTransactionStatus_EnumHasExactlyThreeValues()
    {
        var values = Enum.GetValues<RecurringTransactionStatus>();

        values.Should().HaveCount(3);
        values.Should().Contain(RecurringTransactionStatus.Active);
        values.Should().Contain(RecurringTransactionStatus.Paused);
        values.Should().Contain(RecurringTransactionStatus.Cancelled);

        Enum.TryParse<RecurringTransactionStatus>("Completed", out _).Should().BeFalse();
    }

    [Fact]
    public async Task RecurringTransaction_EfCoreRoundTrip_PersistedFieldsMatch()
    {
        var options = new DbContextOptionsBuilder<FinanceTrackerContext>()
            .UseInMemoryDatabase("RoundTrip_" + Guid.NewGuid())
            .Options;

        var categoryId = Guid.NewGuid();
        var frequencyId = Guid.Parse("00000000-0000-0000-0000-000000000004");
        var entityId = Guid.NewGuid();

        using (var context = new FinanceTrackerContext(options))
        {
            context.Categories.Add(new Category
            {
                Id = categoryId,
                Name = "Utilities",
                CategoryType = CategoryType.Expense,
                UserId = TestCurrentUserAccessor.DefaultUserId,
                CreatedAt = DateTime.UtcNow
            });

            context.Frequencies.Add(new Frequency
            {
                Id = frequencyId,
                Name = "Monthly",
                Type = FrequencyType.Monthly,
                IntervalDays = 30,
                CreatedAt = DateTime.UtcNow
            });

            await context.SaveChangesAsync();
        }

        using (var context = new FinanceTrackerContext(options))
        {
            var entity = new RecurringTransaction
            {
                Id = entityId,
                Name = "Electric Bill",
                DefaultAmount = 85.50m,
                CategoryId = categoryId,
                Category = context.Categories.Find(categoryId)!,
                UserId = TestCurrentUserAccessor.DefaultUserId,
                FrequencyId = frequencyId,
                Frequency = context.Frequencies.Find(frequencyId)!,
                StartDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                NextOccurrenceDate = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
                Status = RecurringTransactionStatus.Active,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "test"
            };

            context.RecurringTransactions.Add(entity);
            await context.SaveChangesAsync();
        }

        using (var context = new FinanceTrackerContext(options))
        {
            var retrieved = await context.RecurringTransactions.FindAsync(entityId);

            retrieved.Should().NotBeNull();
            retrieved!.Name.Should().Be("Electric Bill");
            retrieved.DefaultAmount.Should().Be(85.50m);
            retrieved.CategoryId.Should().Be(categoryId);
            retrieved.FrequencyId.Should().Be(frequencyId);
            retrieved.Status.Should().Be(RecurringTransactionStatus.Active);
        }
    }

    [Fact]
    public async Task Transaction_WithNullRecurringTransactionId_IsSavedAndRetrievedAsStandalone()
    {
        var options = new DbContextOptionsBuilder<FinanceTrackerContext>()
            .UseInMemoryDatabase("Standalone_" + Guid.NewGuid())
            .Options;

        var categoryId = Guid.NewGuid();
        var transactionId = Guid.NewGuid();

        using (var context = new FinanceTrackerContext(options))
        {
            context.Categories.Add(new Category
            {
                Id = categoryId,
                Name = "Food",
                CategoryType = CategoryType.Expense,
                UserId = TestCurrentUserAccessor.DefaultUserId,
                CreatedAt = DateTime.UtcNow
            });

            await context.SaveChangesAsync();
        }

        using (var context = new FinanceTrackerContext(options))
        {
            var transaction = new Transaction
            {
                Id = transactionId,
                Name = "Coffee",
                CategoryId = categoryId,
                Category = context.Categories.Find(categoryId)!,
                UserId = TestCurrentUserAccessor.DefaultUserId,
                Amount = 4.50m,
                TransactionDate = DateTime.UtcNow,
                RecurringTransactionId = null,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "test"
            };

            context.Transactions.Add(transaction);
            await context.SaveChangesAsync();
        }

        using (var context = new FinanceTrackerContext(options))
        {
            var retrieved = await context.Transactions.FindAsync(transactionId);

            retrieved.Should().NotBeNull();
            retrieved!.RecurringTransactionId.Should().BeNull();
        }
    }
}
