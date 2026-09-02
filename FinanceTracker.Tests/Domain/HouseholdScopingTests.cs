using FinanceTracker.Domain.Entities;
using FinanceTracker.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Tests.Domain;

/// <summary>
/// The household half of the tenancy filter, at the level the filter actually lives.
///
/// <see cref="QueryFilterProbeTests"/> pins down that the filter hides another tenant's rows;
/// these pin down the one case where it deliberately does not — a row stamped with the
/// household the caller belongs to — and, just as importantly, the cases that look similar
/// and must still be hidden.
/// </summary>
public class HouseholdScopingTests
{
    private static readonly Guid Alice = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid Bob = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid Stranger = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    private static readonly Guid OurHousehold = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly Guid OtherHousehold = Guid.Parse("66666666-7777-8888-9999-000000000000");

    private static DbContextOptions<FinanceTrackerContext> SharedStore(string name) =>
        new DbContextOptionsBuilder<FinanceTrackerContext>().UseInMemoryDatabase(name).Options;

    private static FinanceTrackerContext As(
        DbContextOptions<FinanceTrackerContext> options,
        Guid? userId,
        Guid? householdId = null) =>
        new(options, new TestCurrentUserAccessor(userId, householdId));

    private static Category CategoryFor(Guid userId, Guid? householdId, string name) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        CategoryType = CategoryType.Expense,
        UserId = userId,
        HouseholdId = householdId
    };

    private static Transaction TransactionFor(Guid userId, Guid? householdId, Guid categoryId, string name) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        CategoryId = categoryId,
        Category = null!,
        UserId = userId,
        HouseholdId = householdId,
        Amount = 10m,
        TransactionDate = DateTime.UtcNow
    };

    /// <summary>Bob's shared row, Bob's private row, and a stranger's row in another household.</summary>
    private static async Task SeedAsync(DbContextOptions<FinanceTrackerContext> options)
    {
        using var context = As(options, userId: null);

        var shared = CategoryFor(Bob, OurHousehold, "Shared groceries");
        var private_ = CategoryFor(Bob, null, "Bob's private hobby");
        var elsewhere = CategoryFor(Stranger, OtherHousehold, "Someone else's rent");

        context.Categories.AddRange(shared, private_, elsewhere);
        context.Transactions.AddRange(
            TransactionFor(Bob, OurHousehold, shared.Id, "Bob's shop"),
            TransactionFor(Bob, null, private_.Id, "Bob's secret"),
            TransactionFor(Stranger, OtherHousehold, elsewhere.Id, "Stranger's rent"));

        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task AMemberSeesRowsStampedWithTheirHousehold()
    {
        var options = SharedStore($"Household_Sees_{Guid.NewGuid()}");
        await SeedAsync(options);

        using var asAlice = As(options, Alice, OurHousehold);

        var names = await asAlice.Transactions.Select(t => t.Name).ToListAsync();

        names.Should().Contain("Bob's shop", "that is the whole point of a household");
    }

    [Fact]
    public async Task AMemberDoesNotSeeARowTheirHouseholdmateNeverShared()
    {
        // Bob wrote this before joining, or after leaving. Either way it carries no household
        // and stays his alone — sharing is a property of the row, not of the person.
        var options = SharedStore($"Household_Private_{Guid.NewGuid()}");
        await SeedAsync(options);

        using var asAlice = As(options, Alice, OurHousehold);

        var names = await asAlice.Transactions.Select(t => t.Name).ToListAsync();

        names.Should().NotContain("Bob's secret");
    }

    [Fact]
    public async Task AMemberDoesNotSeeAnotherHouseholdsRows()
    {
        var options = SharedStore($"Household_Other_{Guid.NewGuid()}");
        await SeedAsync(options);

        using var asAlice = As(options, Alice, OurHousehold);

        var names = await asAlice.Transactions.Select(t => t.Name).ToListAsync();

        names.Should().NotContain("Stranger's rent");
    }

    [Fact]
    public async Task SomeoneInNoHouseholdSeesNothingShared()
    {
        // The guard on CurrentHouseholdId, stated as a test: a null household must never
        // match rows whose HouseholdId is also null but whose owner is somebody else.
        var options = SharedStore($"Household_None_{Guid.NewGuid()}");
        await SeedAsync(options);

        using var asAlice = As(options, Alice, householdId: null);

        (await asAlice.Transactions.ToListAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task OwnRowsStayVisibleWhileInAHousehold()
    {
        // The filter is two ways in, not a replacement. A member whose own rows are not yet
        // stamped — mid-join, or written before the backfill — must still see them.
        var options = SharedStore($"Household_Own_{Guid.NewGuid()}");
        await SeedAsync(options);

        using (var setup = As(options, userId: null))
        {
            var mine = CategoryFor(Alice, null, "Alice's own");
            setup.Categories.Add(mine);
            setup.Transactions.Add(TransactionFor(Alice, null, mine.Id, "Alice's unstamped row"));
            await setup.SaveChangesAsync();
        }

        using var asAlice = As(options, Alice, OurHousehold);

        var names = await asAlice.Transactions.Select(t => t.Name).ToListAsync();

        names.Should().Contain("Alice's unstamped row");
    }

    [Fact]
    public async Task CategoriesFollowTheSameRuleAsTransactions()
    {
        // Stated separately because the filter is declared three times, once per entity, and
        // a household added to two of them would fail nowhere else.
        var options = SharedStore($"Household_Categories_{Guid.NewGuid()}");
        await SeedAsync(options);

        using var asAlice = As(options, Alice, OurHousehold);

        var names = await asAlice.Categories.Select(c => c.Name).ToListAsync();

        names.Should().Contain("Shared groceries");
        names.Should().NotContain("Bob's private hobby");
        names.Should().NotContain("Someone else's rent");
    }

    [Fact]
    public async Task RecurringTemplatesFollowTheSameRuleAsTransactions()
    {
        var options = SharedStore($"Household_Recurring_{Guid.NewGuid()}");
        var frequency = new Frequency { Id = Guid.NewGuid(), Name = "Monthly", Type = FrequencyType.Monthly };
        var category = CategoryFor(Bob, OurHousehold, "Bills");

        using (var setup = As(options, userId: null))
        {
            setup.Frequencies.Add(frequency);
            setup.Categories.Add(category);
            setup.RecurringTransactions.Add(new RecurringTransaction
            {
                Id = Guid.NewGuid(),
                Name = "Shared rent",
                DefaultAmount = 1200m,
                CategoryId = category.Id,
                Category = null!,
                UserId = Bob,
                HouseholdId = OurHousehold,
                FrequencyId = frequency.Id,
                Frequency = null!,
                StartDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                NextOccurrenceDate = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
                Status = RecurringTransactionStatus.Active
            });

            await setup.SaveChangesAsync();
        }

        using var asAlice = As(options, Alice, OurHousehold);

        (await asAlice.RecurringTransactions.Select(r => r.Name).ToListAsync())
            .Should().ContainSingle().Which.Should().Be("Shared rent");
    }
}
