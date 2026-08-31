using FinanceTracker.Application.Dtos;
using FinanceTracker.Application.Features.RecurringTransactions;
using FinanceTracker.Application.Features.RecurringTransactions.Commands.CreateRecurringTransaction;
using FinanceTracker.Domain.Entities;
using FinanceTracker.Domain.Services;
using FinanceTracker.Domain.Repositories;
using FluentAssertions;
using Moq;

namespace FinanceTracker.Tests.Unit.Handlers;

public class CreateRecurringTransactionCommandHandlerTests
{
    private readonly Mock<IRecurringTransactionRepository> _templates = new();
    private readonly Mock<ICategoryRepository> _categories = new();
    private readonly Mock<IFrequencyRepository> _frequencies = new();

    private readonly Category _category = RecurringTemplateFactory.ExpenseCategory();
    private readonly Frequency _frequency = RecurringTemplateFactory.Monthly();

    private RecurringTransaction? _saved;

    public CreateRecurringTransactionCommandHandlerTests()
    {
        _categories.Setup(c => c.GetByIdAsync(_category.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_category);
        _frequencies.Setup(f => f.GetByIdAsync(_frequency.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_frequency);

        _templates
            .Setup(t => t.AddAsync(It.IsAny<RecurringTransaction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RecurringTransaction t, CancellationToken _) =>
            {
                _saved = t;
                return t;
            });

        // Mirrors the real repository re-reading the row with its navigations loaded.
        _templates
            .Setup(t => t.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                if (_saved is null) return null;
                _saved.Category = _category;
                _saved.Frequency = _frequency;
                return _saved;
            });
    }

    private CreateRecurringTransactionCommandHandler Sut(TestCurrentUserAccessor? user = null)
        => new(_templates.Object, _categories.Object, _frequencies.Object, user ?? new TestCurrentUserAccessor());

    private CreateRecurringTransactionDto Dto(DateTime startDate, DateTime? endDate = null) => new()
    {
        Name = "Rent",
        Description = "Monthly rent",
        Amount = 1200m,
        CategoryId = _category.Id,
        FrequencyId = _frequency.Id,
        StartDate = startDate,
        EndDate = endDate
    };

    private Task<RecurringTransactionCommandResult> Handle(CreateRecurringTransactionDto dto)
        => Sut().Handle(new CreateRecurringTransactionCommand(dto), CancellationToken.None);

    [Fact]
    public async Task Handle_WithAFutureStartDate_SchedulesTheFirstOccurrenceOnThatDate()
    {
        var start = DateTime.UtcNow.AddDays(10);

        var result = await Handle(Dto(start));

        result.Outcome.Should().Be(RecurringTransactionOutcome.Success);
        result.Data!.NextOccurrenceDate.Should().Be(start);
    }

    [Fact]
    public async Task Handle_WithAStartDateOfToday_SchedulesItToday_NotAPeriodLater()
    {
        // The regression this exists for. A date picker sends a calendar date at midnight.
        // Comparing that against a wall-clock UtcNow made an occurrence due later the same
        // day read as already gone by, so the walk advanced a whole period: a monthly
        // template created at 22:00 for "today" silently lost its first payment.
        //
        // The worker generates on NextOccurrenceDate <= now, so it would have picked today
        // up quite happily — only the seeding disagreed.
        var start = DateTime.UtcNow.Date;

        var result = await Handle(Dto(start));

        result.Outcome.Should().Be(RecurringTransactionOutcome.Success);
        result.Data!.NextOccurrenceDate.Should().Be(start,
            "an occurrence due today has not gone by yet");
    }

    [Fact]
    public async Task Handle_WithAPastStartDate_SchedulesForwardInsteadOfBackfilling()
    {
        // Five months of history. NextOccurrenceDate = StartDate would hand the worker's
        // catch-up loop five real transactions to write on its next run.
        var start = DateTime.UtcNow.AddMonths(-5);

        var result = await Handle(Dto(start));

        result.Outcome.Should().Be(RecurringTransactionOutcome.Success);
        result.Data!.NextOccurrenceDate.Should().BeOnOrAfter(DateTime.UtcNow.Date,
            "a past start date is an anchor, not a backlog to materialise");
        result.Data.StartDate.Should().Be(start, "the anchor itself is kept, only the schedule moves");
    }

    [Fact]
    public async Task Handle_DerivesTheScheduleThroughTheCalculator_KeepingTheSnapBackAnchor()
    {
        // Anchored on the 31st and started over a year ago, so the sequence has crossed at
        // least one February. Two assertions, because either alone is weak: the first pins
        // the handler to the shared walk (which RecurrenceScheduleTests proves preserves the
        // anchor), the second states the consequence in the handler's own terms.
        var start = new DateTime(DateTime.UtcNow.Year - 1, 1, 31, 0, 0, 0, DateTimeKind.Utc);

        var result = await Handle(Dto(start));

        result.Outcome.Should().Be(RecurringTransactionOutcome.Success);

        var expected = RecurrenceSchedule.FirstDueOnOrAfter(
            FrequencyType.Monthly, _frequency.IntervalDays, start, start, DateTime.UtcNow.Date);

        var next = result.Data!.NextOccurrenceDate;
        next.Should().Be(expected, "the date maths belongs to RecurrenceCalculator, reached through RecurrenceSchedule");
        next.Day.Should().Be(Math.Min(31, DateTime.DaysInMonth(next.Year, next.Month)),
            "the sequence stays anchored on StartDate.Day rather than drifting to whatever a short month clamped it to");
    }

    [Fact]
    public async Task Handle_TakesOwnershipFromTheToken_NotTheRequest()
    {
        await Handle(Dto(DateTime.UtcNow.AddDays(1)));

        _saved.Should().NotBeNull();
        _saved!.UserId.Should().Be(TestCurrentUserAccessor.DefaultUserId);
        _saved.CreatedBy.Should().Be(TestCurrentUserAccessor.DefaultEmail);
    }

    [Fact]
    public async Task Handle_WithNoUserInScope_FailsClosed()
    {
        var sut = Sut(new TestCurrentUserAccessor(null));

        var act = () => sut.Handle(new CreateRecurringTransactionCommand(Dto(DateTime.UtcNow.AddDays(1))), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        _templates.Verify(t => t.AddAsync(It.IsAny<RecurringTransaction>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_AlwaysStartsActive()
    {
        var result = await Handle(Dto(DateTime.UtcNow.AddDays(1)));

        result.Data!.Status.Should().Be(RecurringTransactionStatus.Active);
    }

    [Fact]
    public async Task Handle_WithACategoryTheCallerCannotSee_IsInvalidAndWritesNothing()
    {
        // The tenancy filter is what makes the lookup return null for someone else's
        // category, so this is the create-path tenancy check.
        var dto = Dto(DateTime.UtcNow.AddDays(1));
        dto.CategoryId = Guid.NewGuid();

        var result = await Handle(dto);

        result.Outcome.Should().Be(RecurringTransactionOutcome.Invalid);
        result.Message.Should().Contain("Category");
        _templates.Verify(t => t.AddAsync(It.IsAny<RecurringTransaction>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithAnUnknownFrequency_IsInvalid()
    {
        var dto = Dto(DateTime.UtcNow.AddDays(1));
        dto.FrequencyId = Guid.NewGuid();

        var result = await Handle(dto);

        result.Outcome.Should().Be(RecurringTransactionOutcome.Invalid);
        _templates.Verify(t => t.AddAsync(It.IsAny<RecurringTransaction>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithEndDateBeforeStartDate_IsInvalid()
    {
        var start = DateTime.UtcNow.AddDays(10);

        var result = await Handle(Dto(start, start.AddDays(-1)));

        result.Outcome.Should().Be(RecurringTransactionOutcome.Invalid);
        result.Message.Should().Contain("EndDate");
    }

    [Fact]
    public async Task Handle_WithAWindowThatHasAlreadyClosed_IsInvalid()
    {
        // Start and end both in the past: the template could never generate anything, so
        // creating it Active would be quietly useless.
        var result = await Handle(Dto(DateTime.UtcNow.AddMonths(-6), DateTime.UtcNow.AddMonths(-3)));

        result.Outcome.Should().Be(RecurringTransactionOutcome.Invalid);
        _templates.Verify(t => t.AddAsync(It.IsAny<RecurringTransaction>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithACustomFrequencyMissingItsInterval_IsInvalidRatherThanThrowing()
    {
        var broken = new Frequency
        {
            Id = Guid.NewGuid(),
            Name = "Broken custom",
            Type = FrequencyType.Custom,
            IntervalDays = null,
            IsActive = true
        };
        _frequencies.Setup(f => f.GetByIdAsync(broken.Id, It.IsAny<CancellationToken>())).ReturnsAsync(broken);

        var dto = Dto(DateTime.UtcNow.AddMonths(-2));
        dto.FrequencyId = broken.Id;

        var result = await Handle(dto);

        result.Outcome.Should().Be(RecurringTransactionOutcome.Invalid);
    }
}
