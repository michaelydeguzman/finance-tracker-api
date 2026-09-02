using FinanceTracker.Domain.Services;
using FinanceTracker.Application.Services;

namespace FinanceTracker.Tests;

/// <summary>
/// Fixed-tenant <see cref="ICurrentUserAccessor"/> for tests. Construct with no argument
/// for an arbitrary signed-in user, or with <c>null</c> to assert the fail-closed path
/// taken when no identity is in scope.
/// </summary>
public sealed class TestCurrentUserAccessor : ICurrentUserAccessor
{
    public static readonly Guid DefaultUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public const string DefaultEmail = "test-user@example.com";

    public TestCurrentUserAccessor() => UserId = DefaultUserId;

    public TestCurrentUserAccessor(Guid? userId) => UserId = userId;

    public TestCurrentUserAccessor(Guid? userId, Guid? householdId)
    {
        UserId = userId;
        HouseholdId = householdId;
    }

    public Guid? UserId { get; }

    public string? Email => UserId is null ? null : DefaultEmail;

    /// <summary>
    /// The household half of the tenancy filter. Null unless a test says otherwise, so every
    /// existing test keeps meaning "this user, on their own".
    /// </summary>
    public Guid? HouseholdId { get; }
}
