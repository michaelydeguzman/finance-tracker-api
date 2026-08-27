using FinanceTracker.Domain.Entities;

namespace FinanceTracker.Tests;

/// <summary>
/// Builds a fully-populated recurring template for handler tests, where the repository is a
/// mock and nothing loads the navigations for us.
/// </summary>
internal static class RecurringTemplateFactory
{
    public static readonly Guid MonthlyFrequencyId = Guid.Parse("00000000-0000-0000-0000-000000000004");

    public static Frequency Monthly() => new()
    {
        Id = MonthlyFrequencyId,
        Name = "Monthly",
        Type = FrequencyType.Monthly,
        IntervalDays = 30,
        IsActive = true
    };

    public static Category ExpenseCategory(Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        Name = "Bills",
        CategoryType = CategoryType.Expense,
        UserId = TestCurrentUserAccessor.DefaultUserId,
        IsActive = true
    };

    public static RecurringTransaction Template(
        RecurringTransactionStatus status,
        DateTime startDate,
        DateTime nextOccurrenceDate,
        DateTime? endDate = null,
        Frequency? frequency = null,
        Guid? id = null)
    {
        var category = ExpenseCategory();
        var freq = frequency ?? Monthly();

        return new RecurringTransaction
        {
            Id = id ?? Guid.NewGuid(),
            Name = "Rent",
            Description = "Monthly rent",
            DefaultAmount = 1200m,
            CategoryId = category.Id,
            Category = category,
            UserId = TestCurrentUserAccessor.DefaultUserId,
            FrequencyId = freq.Id,
            Frequency = freq,
            StartDate = startDate,
            EndDate = endDate,
            NextOccurrenceDate = nextOccurrenceDate,
            Status = status,
            CreatedAt = startDate,
            CreatedBy = TestCurrentUserAccessor.DefaultEmail
        };
    }
}
