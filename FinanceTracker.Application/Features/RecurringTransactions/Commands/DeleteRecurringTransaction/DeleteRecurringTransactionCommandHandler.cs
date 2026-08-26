using FinanceTracker.Infrastructure.Persistence;
using MediatR;

namespace FinanceTracker.Application.Features.RecurringTransactions.Commands.DeleteRecurringTransaction;

/// <summary>
/// Removes a template outright — but only one that has never generated anything.
/// </summary>
public sealed class DeleteRecurringTransactionCommandHandler
    : IRequestHandler<DeleteRecurringTransactionCommand, RecurringTransactionCommandResult>
{
    private readonly IRecurringTransactionRepository _templates;

    public DeleteRecurringTransactionCommandHandler(IRecurringTransactionRepository templates)
        => _templates = templates;

    public async Task<RecurringTransactionCommandResult> Handle(
        DeleteRecurringTransactionCommand request,
        CancellationToken cancellationToken)
    {
        var template = await _templates.GetTrackedByIdAsync(request.Id);
        if (template is null)
            return RecurringTransactionCommandResult.NotFound();

        // Transaction.RecurringTransactionId is nullable with OnDelete(SetNull), so a hard
        // delete would succeed silently and strand every row it generated: the money and the
        // dates survive, the reason they exist does not. On real financial records that is a
        // quiet rewrite of history, and nothing in the UI would show it happened.
        //
        // A template that has generated nothing has no history to protect — that is the
        // just-created typo, and deleting it is exactly right. Everything else is asked to
        // cancel instead, which keeps the row and its links while stopping generation.
        var generated = await _templates.CountGeneratedTransactionsAsync(template.Id);
        if (generated > 0)
        {
            return RecurringTransactionCommandResult.Conflict(
                $"This recurring transaction has already generated {generated} transaction(s). " +
                "Cancel it instead, so those transactions keep their history.");
        }

        await _templates.DeleteAsync(template);

        return new RecurringTransactionCommandResult(
            RecurringTransactionOutcome.Success,
            null,
            "Recurring transaction deleted successfully.");
    }
}
