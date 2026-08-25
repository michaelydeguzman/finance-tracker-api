using FinanceTracker.Domain.Entities;
using FinanceTracker.Domain.Services;
using FinanceTracker.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Tests.Domain;

/// <summary>
/// Pins down which EF Core access paths honour the tenancy query filter. FindAsync in
/// particular is a documented exception in some versions, and the repositories use it on
/// their update and delete paths — so this is load-bearing, not trivia.
/// </summary>
public class QueryFilterProbeTests
{
    private static readonly Guid Owner = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid Intruder = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private sealed class FixedUser : ICurrentUserAccessor
    {
        public FixedUser(Guid? userId) => UserId = userId;
        public Guid? UserId { get; }
    }

    private static DbContextOptions<FinanceTrackerContext> SharedStore(string name) =>
        new DbContextOptionsBuilder<FinanceTrackerContext>().UseInMemoryDatabase(name).Options;

    private static async Task<Guid> SeedOwnersTransactionAsync(DbContextOptions<FinanceTrackerContext> options)
    {
        using var context = new FinanceTrackerContext(options, new FixedUser(Owner));
        var category = new Category
        {
            Id = Guid.NewGuid(), Name = "Rent", CategoryType = CategoryType.Expense, UserId = Owner
        };
        var transaction = new Transaction
        {
            Id = Guid.NewGuid(), Name = "March rent", CategoryId = category.Id, Category = null!,
            UserId = Owner, Amount = 1200m, TransactionDate = DateTime.UtcNow
        };
        context.Categories.Add(category);
        context.Transactions.Add(transaction);
        await context.SaveChangesAsync();
        return transaction.Id;
    }

    [Fact]
    public async Task LinqQuery_HidesAnotherTenantsRow()
    {
        var options = SharedStore($"Probe_Linq_{Guid.NewGuid()}");
        var id = await SeedOwnersTransactionAsync(options);

        using var asIntruder = new FinanceTrackerContext(options, new FixedUser(Intruder));

        (await asIntruder.Transactions.FirstOrDefaultAsync(t => t.Id == id)).Should().BeNull();
    }

    [Fact]
    public async Task FindAsync_AlsoHidesAnotherTenantsRow()
    {
        // If this fails, every repository update and delete path is a cross-tenant write.
        var options = SharedStore($"Probe_Find_{Guid.NewGuid()}");
        var id = await SeedOwnersTransactionAsync(options);

        using var asIntruder = new FinanceTrackerContext(options, new FixedUser(Intruder));

        (await asIntruder.Transactions.FindAsync(id)).Should().BeNull();
    }

    [Fact]
    public async Task NoUserContext_SeesNothingRatherThanEverything()
    {
        var options = SharedStore($"Probe_NoUser_{Guid.NewGuid()}");
        await SeedOwnersTransactionAsync(options);

        using var anonymous = new FinanceTrackerContext(options, currentUser: null);

        (await anonymous.Transactions.ToListAsync()).Should().BeEmpty("the filter fails closed");
    }

    [Fact]
    public async Task IgnoreQueryFilters_IsTheDeliberateCrossTenantEscapeHatch()
    {
        var options = SharedStore($"Probe_Ignore_{Guid.NewGuid()}");
        await SeedOwnersTransactionAsync(options);

        using var worker = new FinanceTrackerContext(options, currentUser: null);

        (await worker.Transactions.IgnoreQueryFilters().ToListAsync()).Should().ContainSingle();
    }
}
