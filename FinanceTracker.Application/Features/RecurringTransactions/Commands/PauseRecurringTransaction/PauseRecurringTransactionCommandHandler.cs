using FinanceTracker.Application.Dtos.Responses;
using FinanceTracker.Domain.Entities;
using FinanceTracker.Infrastructure.Persistence;
using MediatR;

namespace FinanceTracker.Application.Features.RecurringTransactions.Commands.PauseRecurringTransaction;

/// <summary>
/// Stops the worker picking the template up, without touching its schedule.
///
/// <c>NextOccurrenceDate</c> is deliberately left where it is: resuming is what decides what
/// happens to a schedule that went stale while paused, and moving the date here would take
/// that decision away from the point where the user can see it.
/// </summary>
public sealed class PauseRecurringTransactionCommandHandler
    : IRequestHandler<PauseRecurringTransactionCommand, RecurringTransactionCommandResult>
{
    private readonly IRecurringTransactionRepository _templates;

    public PauseRecurringTransactionCommandHandler(IRecurringTransactionRepository templates)
        => _templates = templates;

    public async Task<RecurringTransactionCommandResult> Handle(
        PauseRecurringTransactionCommand request,
        CancellationToken cancellationToken)
    {
        var template = await _templates.GetTrackedByIdAsync(request.Id);
        if (template is null)
            return RecurringTransactionCommandResult.NotFound();

        switch (template.Status)
        {
            case RecurringTransactionStatus.Cancelled:
                return RecurringTransactionCommandResult.Conflict(
                    "A cancelled recurring transaction cannot be paused.");

            // Already paused: report success rather than an error. A double-click on the
            // pause button asked for a state the template is already in.
            case RecurringTransactionStatus.Paused:
                return RecurringTransactionCommandResult.Success(
                    RecurringTransactionResponseDto.FromEntity(template));
        }

        template.Status = RecurringTransactionStatus.Paused;
        await _templates.SaveChangesAsync();

        return RecurringTransactionCommandResult.Success(
            RecurringTransactionResponseDto.FromEntity(template));
    }
}
