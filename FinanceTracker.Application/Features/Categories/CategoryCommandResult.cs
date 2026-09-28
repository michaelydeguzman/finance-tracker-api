using FinanceTracker.Application.Dtos.Responses;

namespace FinanceTracker.Application.Features.Categories;

/// <summary>
/// Why a category write did not simply succeed. Same shape as
/// <see cref="RecurringTransactions.RecurringTransactionCommandResult"/>: a name already taken
/// and a category still in use are both refusals, and neither is a "not found".
/// </summary>
public enum CategoryOutcome
{
    Success,

    /// <summary>No such category *for this caller*. Maps to 404.</summary>
    NotFound,

    /// <summary>Asked for something a category may never do, such as change its type. Maps to 400.</summary>
    Invalid,

    /// <summary>The name is taken, or the category is still in use. Maps to 409.</summary>
    Conflict
}

public sealed record CategoryCommandResult(
    CategoryOutcome Outcome,
    CategoryResponseDto? Data = null,
    string? Message = null)
{
    public static CategoryCommandResult Success(CategoryResponseDto? data = null)
        => new(CategoryOutcome.Success, data);

    public static CategoryCommandResult NotFound(string message = "Category not found.")
        => new(CategoryOutcome.NotFound, null, message);

    public static CategoryCommandResult Invalid(string message)
        => new(CategoryOutcome.Invalid, null, message);

    public static CategoryCommandResult Conflict(string message)
        => new(CategoryOutcome.Conflict, null, message);
}
