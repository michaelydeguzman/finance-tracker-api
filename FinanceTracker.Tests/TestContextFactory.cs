using FinanceTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Tests;

/// <summary>
/// Builds an InMemory context bound to a tenant. Tests that read back what they wrote need
/// this: the tenancy query filter fails closed, so a context with no user sees nothing.
/// </summary>
public static class TestContextFactory
{
    public static FinanceTrackerContext ForDefaultUser(string databaseName) =>
        For(databaseName, new TestCurrentUserAccessor());

    public static FinanceTrackerContext For(string databaseName, TestCurrentUserAccessor accessor) =>
        new(
            new DbContextOptionsBuilder<FinanceTrackerContext>()
                .UseInMemoryDatabase(databaseName)
                .Options,
            accessor);
}
