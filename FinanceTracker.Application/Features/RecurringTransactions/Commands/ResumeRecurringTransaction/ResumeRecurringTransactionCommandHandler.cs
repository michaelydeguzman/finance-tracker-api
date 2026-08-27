using FinanceTracker.Application.Dtos.Responses;
using FinanceTracker.Domain.Entities;
using FinanceTracker.Domain.Services;
using FinanceTracker.Infrastructure.Persistence;
using MediatR;

namespace FinanceTracker.Application.Features.RecurringTransactions.Commands.ResumeRecurringTransaction;

/// <summary>
/// Puts a paused template back in front of the worker — and fast-forwards its schedule first.
/// </summary>
public sealed class ResumeRecurringTransactionCommandHandler
    : IRequestHandler<ResumeRecurringTransactionCommand, RecurringTransactionCommandResult>
{
    private readonly IRecurringTransactionRepository _templates;

    public ResumeRecurringTransactionCommandHandler(IRecurringTransactionRepository templates)
        => _templates = templates;

    public async Task<RecurringTransactionCommandResult> Handle(
        ResumeRecurringTransactionCommand request,
        CancellationToken cancellationToken)
    {
        var template = await _templates.GetTrackedByIdAsync(request.Id);
        if (template is null)
            return RecurringTransactionCommandResult.NotFound();

        switch (template.Status)
        {
            case RecurringTransactionStatus.Cancelled:
                // Cancelling is terminal on purpose. If it could be undone it would be
                // indistinguishable from pausing, and the two buttons would mean the same
                // thing. Starting again is a new template — which is also the honest answer
                // for the schedule, since a fresh StartDate is the anchor the user wants.
                return RecurringTransactionCommandResult.Conflict(
                    "A cancelled recurring transaction cannot be resumed. Create a new one instead.");

            // Already running. Nothing to do, and specifically no fast-forward: rolling the
            // schedule here would skip an occurrence that is legitimately due right now.
            case RecurringTransactionStatus.Active:
                return RecurringTransactionCommandResult.Success(
                    RecurringTransactionResponseDto.FromEntity(template));
        }

        var frequency = template.Frequency;

        if (frequency.Type == FrequencyType.Custom && frequency.IntervalDays is not > 0)
        {
            return RecurringTransactionCommandResult.Conflict(
                "The template's frequency is custom but has no positive interval configured, so its schedule cannot be advanced.");
        }

        // The decision this endpoint exists to make.
        //
        // A template paused in January and resumed in June has a NextOccurrenceDate five
        // months in the past. The worker's catch-up loop is unconditional: leaving the date
        // alone would make it materialise every missed occurrence on its next run — five
        // real financial records for a period the user had deliberately switched off.
        //
        // So resuming means "carry on from the next occurrence", not "settle the arrears".
        // Pausing is the user saying these should not be recorded; honouring that is the
        // whole point of the feature. Anything genuinely missed can be entered by hand,
        // which is far easier than finding and deleting five rows that appeared unbidden.
        //
        // The walk goes through RecurrenceSchedule, so the sequence stays anchored on
        // StartDate and a Jan-31 monthly template still snaps back to the 31st.
        var resumedFrom = RecurrenceSchedule.FirstDueOnOrAfter(
            frequency.Type,
            frequency.IntervalDays,
            template.StartDate,
            template.NextOccurrenceDate,
            DateTime.UtcNow);

        if (template.EndDate is { } end && resumedFrom > end)
        {
            // Resuming would produce an Active template the worker can never generate from,
            // which reads as broken rather than finished. Say so instead.
            return RecurringTransactionCommandResult.Conflict(
                "This recurring transaction has no occurrence left before its end date, so resuming it would generate nothing.");
        }

        template.NextOccurrenceDate = resumedFrom;
        template.Status = RecurringTransactionStatus.Active;
        await _templates.SaveChangesAsync();

        return RecurringTransactionCommandResult.Success(
            RecurringTransactionResponseDto.FromEntity(template));
    }
}
