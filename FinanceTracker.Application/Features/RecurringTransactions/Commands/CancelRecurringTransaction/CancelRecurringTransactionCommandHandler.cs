using FinanceTracker.Application.Dtos.Responses;
using FinanceTracker.Domain.Entities;
using FinanceTracker.Domain.Repositories;
using MediatR;

namespace FinanceTracker.Application.Features.RecurringTransactions.Commands.CancelRecurringTransaction;

/// <summary>
/// Retires a template for good, keeping the row so the transactions it generated still have
/// something to point at. This is not a delete, and it is not reversible — see
/// <c>ResumeRecurringTransactionCommandHandler</c> for why cancelling stays terminal.
/// </summary>
public sealed class CancelRecurringTransactionCommandHandler
    : IRequestHandler<CancelRecurringTransactionCommand, RecurringTransactionCommandResult>
{
    private readonly IRecurringTransactionRepository _templates;

    public CancelRecurringTransactionCommandHandler(IRecurringTransactionRepository templates)
        => _templates = templates;

    public async Task<RecurringTransactionCommandResult> Handle(
        CancelRecurringTransactionCommand request,
        CancellationToken cancellationToken)
    {
        var template = await _templates.GetTrackedByIdAsync(request.Id);
        if (template is null)
            return RecurringTransactionCommandResult.NotFound();

        // Cancelling an already-cancelled template is what the caller asked for, so it is a
        // success. Both Active and Paused cancel normally.
        if (template.Status != RecurringTransactionStatus.Cancelled)
        {
            template.Status = RecurringTransactionStatus.Cancelled;
            await _templates.SaveChangesAsync();
        }

        return RecurringTransactionCommandResult.Success(
            RecurringTransactionResponseDto.FromEntity(template));
    }
}
