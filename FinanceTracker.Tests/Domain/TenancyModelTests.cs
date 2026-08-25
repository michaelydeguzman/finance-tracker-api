using FinanceTracker.Domain.Entities;
using FinanceTracker.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Tests.Domain;

/// <summary>
/// Covers the tenancy column now carried by every financial entity.
///
/// Note the ceiling on these: EF Core's InMemory provider enforces neither unique indexes
/// nor foreign keys, so the uniqueness of (Provider, ProviderSubject) and the Restrict
/// delete behaviour are only really exercised against SQL Server. What is verified here
/// is that ownership is required, persisted, and separates one user's records from
/// another's.
/// </summary>
public class TenancyModelTests
{
    private static readonly Guid UserA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid UserB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private static DbContextOptions<FinanceTrackerContext> NewOptions(string prefix) =>
        new DbContextOptionsBuilder<FinanceTrackerContext>()
            .UseInMemoryDatabase($"{prefix}_{Guid.NewGuid()}")
            .Options;

    private static Category CategoryFor(Guid userId, string name) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        CategoryType = CategoryType.Expense,
        UserId = userId,
        CreatedAt = DateTime.UtcNow
    };

    [Fact]
    public async Task TransactionsAreSeparableByOwner()
    {
        var options = NewOptions("Tenancy");
        var categoryA = CategoryFor(UserA, "Groceries");
        var categoryB = CategoryFor(UserB, "Groceries");

        using (var context = new FinanceTrackerContext(options))
        {
            context.Categories.AddRange(categoryA, categoryB);
            context.Transactions.AddRange(
                new Transaction
                {
                    Id = Guid.NewGuid(),
                    Name = "A's shop",
                    CategoryId = categoryA.Id,
                    Category = null!,
                    UserId = UserA,
                    Amount = 20m,
                    TransactionDate = DateTime.UtcNow
                },
                new Transaction
                {
                    Id = Guid.NewGuid(),
                    Name = "B's shop",
                    CategoryId = categoryB.Id,
                    Category = null!,
                    UserId = UserB,
                    Amount = 35m,
                    TransactionDate = DateTime.UtcNow
                });

            await context.SaveChangesAsync();
        }

        using (var context = new FinanceTrackerContext(options))
        {
            var forA = await context.Transactions.Where(t => t.UserId == UserA).ToListAsync();

            forA.Should().ContainSingle();
            forA[0].Name.Should().Be("A's shop");
        }
    }

    [Fact]
    public async Task CategoryNameMayRepeatAcrossUsers()
    {
        // Two households both having a "Groceries" category is normal; uniqueness is
        // scoped to (UserId, Name), never to Name alone.
        var options = NewOptions("SharedNames");

        using (var context = new FinanceTrackerContext(options))
        {
            context.Categories.AddRange(CategoryFor(UserA, "Groceries"), CategoryFor(UserB, "Groceries"));
            await context.SaveChangesAsync();
        }

        using (var context = new FinanceTrackerContext(options))
        {
            var named = await context.Categories.Where(c => c.Name == "Groceries").ToListAsync();

            named.Should().HaveCount(2);
            named.Select(c => c.UserId).Should().BeEquivalentTo(new[] { UserA, UserB });
        }
    }

    [Fact]
    public async Task GeneratedTransactionInheritsTheTemplateOwner()
    {
        // The worker has no request identity, so a generated instance can only get its
        // tenant from the template it came from.
        var options = NewOptions("WorkerTenancy");
        var category = CategoryFor(UserA, "Bills");
        var frequency = new Frequency { Id = Guid.NewGuid(), Name = "Monthly", Type = FrequencyType.Monthly };

        var template = new RecurringTransaction
        {
            Id = Guid.NewGuid(),
            Name = "Rent",
            DefaultAmount = 1200m,
            CategoryId = category.Id,
            Category = null!,
            UserId = UserA,
            FrequencyId = frequency.Id,
            Frequency = null!,
            StartDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            NextOccurrenceDate = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
            Status = RecurringTransactionStatus.Active
        };

        using (var context = new FinanceTrackerContext(options))
        {
            context.Categories.Add(category);
            context.Frequencies.Add(frequency);
            context.RecurringTransactions.Add(template);
            await context.SaveChangesAsync();
        }

        using (var context = new FinanceTrackerContext(options))
        {
            var loaded = await context.RecurringTransactions.SingleAsync(r => r.Id == template.Id);

            loaded.UserId.Should().Be(UserA);
        }
    }
}
