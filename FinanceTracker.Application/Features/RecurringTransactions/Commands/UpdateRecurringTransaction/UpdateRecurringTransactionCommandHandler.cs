using FinanceTracker.Application.Dtos.Responses;
using FinanceTracker.Domain.Entities;
using FinanceTracker.Domain.Repositories;
using FinanceTracker.Domain.Services;
using MediatR;

namespace FinanceTracker.Application.Features.RecurringTransactions.Commands.UpdateRecurringTransaction;

public sealed class UpdateRecurringTransactionCommandHandler
    : IRequestHandler<UpdateRecurringTransactionCommand, RecurringTransactionCommandResult>
{
    private readonly IRecurringTransactionRepository _templates;
    private readonly ICategoryRepository _categories;
    private readonly IFrequencyRepository _frequencies;

    public UpdateRecurringTransactionCommandHandler(
        IRecurringTransactionRepository templates,
        ICategoryRepository categories,
        IFrequencyRepository frequencies)
    {
        _templates = templates;
        _categories = categories;
        _frequencies = frequencies;
    }

    public async Task<RecurringTransactionCommandResult> Handle(
        UpdateRecurringTransactionCommand request,
        CancellationToken cancellationToken)
    {
        var dto = request.Dto;

        // Tenancy-scoped: another tenant's id resolves to null here and leaves as a 404.
        var template = await _templates.GetTrackedByIdAsync(request.Id);
        if (template is null)
            return RecurringTransactionCommandResult.NotFound();

        if (template.Status == RecurringTransactionStatus.Cancelled)
        {
            // Cancelling is terminal, and past transactions still point at this template as
            // their origin. Editing it would retroactively change what those rows claim to
            // have come from.
            return RecurringTransactionCommandResult.Conflict(
                "A cancelled recurring transaction cannot be edited. Create a new one instead.");
        }

        if (dto.EndDate is { } end && end < dto.StartDate)
            return RecurringTransactionCommandResult.Invalid("EndDate cannot be earlier than StartDate.");

        var category = await _categories.GetByIdAsync(dto.CategoryId);
        if (category is null)
            return RecurringTransactionCommandResult.Invalid("Category not found.");

        var frequency = await _frequencies.GetByIdAsync(dto.FrequencyId);
        if (frequency is null || !frequency.IsActive)
            return RecurringTransactionCommandResult.Invalid("Frequency not found.");

        if (frequency.Type == FrequencyType.Custom && frequency.IntervalDays is not > 0)
        {
            return RecurringTransactionCommandResult.Invalid(
                "The selected frequency is custom but has no positive interval configured.");
        }

        // Only the two fields the schedule is derived from can invalidate it. Recomputing on
        // every edit would be worse than leaving it alone: renaming a template whose next
        // occurrence is due today would silently push that occurrence into the future and
        // skip it.
        var scheduleChanged = dto.StartDate != template.StartDate
                           || dto.FrequencyId != template.FrequencyId;

        template.Name = dto.Name;
        template.Description = dto.Description;
        template.DefaultAmount = dto.Amount;
        template.CategoryId = dto.CategoryId;
        template.FrequencyId = dto.FrequencyId;
        template.StartDate = dto.StartDate;
        template.EndDate = dto.EndDate;

        if (scheduleChanged)
        {
            // Re-anchored on the new start date, through the same calculator, and again
            // without backfilling the interval that has already elapsed.
            template.NextOccurrenceDate = RecurrenceSchedule.FirstDueOnOrAfter(
                frequency.Type, frequency.IntervalDays, dto.StartDate, dto.StartDate, DateTime.UtcNow.Date);
        }

        // An EndDate that now falls before the next occurrence is allowed, unlike at create
        // time: shortening the window is how a user winds a template down after the last
        // occurrence they want, and the worker simply stops generating.
        await _templates.SaveChangesAsync();

        var withRelations = await _templates.GetByIdAsync(template.Id);
        return RecurringTransactionCommandResult.Success(
            RecurringTransactionResponseDto.FromEntity(withRelations ?? template));
    }
}
