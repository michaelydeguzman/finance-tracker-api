using FinanceTracker.Application.Dtos.Responses;

namespace FinanceTracker.Application.Features.RecurringTransactions;

/// <summary>
/// Why a command did not simply succeed. Handlers return this rather than throwing or
/// returning a bare null, because these endpoints have three distinct failure shapes and
/// collapsing them loses real information: "you cannot reach that template" and "that
/// transition is not legal from here" deserve different status codes.
/// </summary>
public enum RecurringTransactionOutcome
{
    Success,

    /// <summary>
    /// No such template *for this caller*. Another tenant's id lands here too — deliberately
    /// indistinguishable from a genuinely missing one, since confirming the id exists would
    /// itself leak something.
    /// </summary>
    NotFound,

    /// <summary>The request was well-formed but referenced something invalid. Maps to 400.</summary>
    Invalid,

    /// <summary>The template's current state forbids this. Maps to 409.</summary>
    Conflict
}

/// <summary>The outcome of a recurring-template command, plus the template when there is one.</summary>
public sealed record RecurringTransactionCommandResult(
    RecurringTransactionOutcome Outcome,
    RecurringTransactionResponseDto? Data = null,
    string? Message = null)
{
    public static RecurringTransactionCommandResult Success(RecurringTransactionResponseDto data)
        => new(RecurringTransactionOutcome.Success, data);

    public static RecurringTransactionCommandResult NotFound(string message = "Recurring transaction not found.")
        => new(RecurringTransactionOutcome.NotFound, null, message);

    public static RecurringTransactionCommandResult Invalid(string message)
        => new(RecurringTransactionOutcome.Invalid, null, message);

    public static RecurringTransactionCommandResult Conflict(string message)
        => new(RecurringTransactionOutcome.Conflict, null, message);
}
