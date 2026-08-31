using FinanceTracker.Application.Dtos.Responses;
using FinanceTracker.Domain.Entities;
using FinanceTracker.Domain.Repositories;
using FinanceTracker.Domain.Services;
using MediatR;

namespace FinanceTracker.Application.Features.RecurringTransactions.Commands.CreateRecurringTransaction;

public sealed class CreateRecurringTransactionCommandHandler
    : IRequestHandler<CreateRecurringTransactionCommand, RecurringTransactionCommandResult>
{
    private readonly IRecurringTransactionRepository _templates;
    private readonly ICategoryRepository _categories;
    private readonly IFrequencyRepository _frequencies;
    private readonly ICurrentUserAccessor _currentUser;

    public CreateRecurringTransactionCommandHandler(
        IRecurringTransactionRepository templates,
        ICategoryRepository categories,
        IFrequencyRepository frequencies,
        ICurrentUserAccessor currentUser)
    {
        _templates = templates;
        _categories = categories;
        _frequencies = frequencies;
        _currentUser = currentUser;
    }

    public async Task<RecurringTransactionCommandResult> Handle(
        CreateRecurringTransactionCommand request,
        CancellationToken cancellationToken)
    {
        var dto = request.Dto;

        if (dto.EndDate is { } end && end < dto.StartDate)
            return RecurringTransactionCommandResult.Invalid("EndDate cannot be earlier than StartDate.");

        // The category lookup is tenancy-scoped, so this doubles as the ownership check:
        // another tenant's category id is simply not found, and no template can be pointed
        // at it.
        var category = await _categories.GetByIdAsync(dto.CategoryId, cancellationToken);
        if (category is null)
            return RecurringTransactionCommandResult.Invalid("Category not found.");

        var frequency = await _frequencies.GetByIdAsync(dto.FrequencyId, cancellationToken);
        if (frequency is null || !frequency.IsActive)
            return RecurringTransactionCommandResult.Invalid("Frequency not found.");

        if (frequency.Type == FrequencyType.Custom && frequency.IntervalDays is not > 0)
        {
            // A reference row this broken would otherwise surface as a 500 from deep inside
            // the calculator.
            return RecurringTransactionCommandResult.Invalid(
                "The selected frequency is custom but has no positive interval configured.");
        }

        // Date, not the instant. StartDate arrives as a calendar date at midnight, so
        // comparing it against a wall-clock UtcNow makes every occurrence due *today*
        // look like it has already passed, and the walk below skips a whole period.
        var now = DateTime.UtcNow.Date;

        // Derived, never supplied. RecurrenceSchedule walks the sequence with
        // RecurrenceCalculator, so a start date of Jan 31 keeps snapping back to the 31st
        // rather than drifting to the 28th — the same invariant the worker depends on.
        //
        // A start date in the past is an anchor, not a backlog: generation begins at the
        // first occurrence still due. Setting NextOccurrenceDate to a past StartDate would
        // hand the worker's catch-up loop months of history to materialise on its next run,
        // which is a bulk write against real financial records triggered by a typo.
        var nextOccurrence = RecurrenceSchedule.FirstDueOnOrAfter(
            frequency.Type, frequency.IntervalDays, dto.StartDate, dto.StartDate, now);

        if (dto.EndDate is { } endDate && nextOccurrence > endDate)
        {
            return RecurringTransactionCommandResult.Invalid(
                "The schedule has no occurrence left before EndDate, so this template could never generate anything.");
        }

        var template = new RecurringTransaction
        {
            Id = Guid.NewGuid(),
            Name = dto.Name,
            Description = dto.Description,
            DefaultAmount = dto.Amount,
            CategoryId = dto.CategoryId,
            Category = null!,
            UserId = _currentUser.RequireUserId(),
            FrequencyId = dto.FrequencyId,
            Frequency = null!,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            NextOccurrenceDate = nextOccurrence,
            Status = RecurringTransactionStatus.Active,
            CreatedAt = now,
            CreatedBy = _currentUser.Email ?? _currentUser.RequireUserId().ToString()
        };

        var created = await _templates.AddAsync(template, cancellationToken);
        var withRelations = await _templates.GetByIdAsync(created.Id, cancellationToken);

        return RecurringTransactionCommandResult.Success(
            RecurringTransactionResponseDto.FromEntity(withRelations ?? created));
    }
}
