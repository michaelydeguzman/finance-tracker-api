namespace FinanceTracker.Application.Features.Households;

/// <summary>
/// Why a household command did not simply succeed. Same shape as
/// <see cref="RecurringTransactions.RecurringTransactionCommandResult"/> and for the same
/// reason — these endpoints fail in genuinely different ways and a bare null would collapse
/// them — with one addition: <see cref="Forbidden"/>.
/// </summary>
public enum HouseholdOutcome
{
    Success,

    /// <summary>No such household or invitation *for this caller*. Maps to 404.</summary>
    NotFound,

    /// <summary>Well-formed but referenced something invalid. Maps to 400.</summary>
    Invalid,

    /// <summary>The current state forbids this — already a member, already invited. Maps to 409.</summary>
    Conflict,

    /// <summary>
    /// The caller is in the household but is not its owner. Distinct from
    /// <see cref="NotFound"/> on purpose: they can already see the household, so answering
    /// 404 would only be confusing, not protective. Maps to 403.
    /// </summary>
    Forbidden
}

public sealed record HouseholdResult<T>(HouseholdOutcome Outcome, T? Data = default, string? Message = null)
{
    public static HouseholdResult<T> Success(T? data = default, string? message = null)
        => new(HouseholdOutcome.Success, data, message);

    public static HouseholdResult<T> NotFound(string message = "Household not found.")
        => new(HouseholdOutcome.NotFound, default, message);

    public static HouseholdResult<T> Invalid(string message)
        => new(HouseholdOutcome.Invalid, default, message);

    public static HouseholdResult<T> Conflict(string message)
        => new(HouseholdOutcome.Conflict, default, message);

    public static HouseholdResult<T> Forbidden(string message = "Only the household owner can do that.")
        => new(HouseholdOutcome.Forbidden, default, message);
}
