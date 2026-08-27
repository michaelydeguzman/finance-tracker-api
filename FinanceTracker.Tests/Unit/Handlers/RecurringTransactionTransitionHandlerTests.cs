using FinanceTracker.Application.Dtos;
using FinanceTracker.Application.Features.RecurringTransactions;
using FinanceTracker.Application.Features.RecurringTransactions.Commands.CancelRecurringTransaction;
using FinanceTracker.Application.Features.RecurringTransactions.Commands.DeleteRecurringTransaction;
using FinanceTracker.Application.Features.RecurringTransactions.Commands.PauseRecurringTransaction;
using FinanceTracker.Application.Features.RecurringTransactions.Commands.ResumeRecurringTransaction;
using FinanceTracker.Application.Features.RecurringTransactions.Commands.UpdateRecurringTransaction;
using FinanceTracker.Domain.Entities;
using FinanceTracker.Infrastructure.Persistence;
using FluentAssertions;
using Moq;

namespace FinanceTracker.Tests.Unit.Handlers;

/// <summary>
/// The rules that decide what a template's status may become, and what that does to its
/// schedule. These are the parts a UI cannot be trusted to enforce.
/// </summary>
public class RecurringTransactionTransitionHandlerTests
{
    private readonly Mock<IRecurringTransactionRepository> _templates = new();
    private readonly Mock<ICategoryRepository> _categories = new();
    private readonly Mock<IFrequencyRepository> _frequencies = new();

    private void Existing(RecurringTransaction template)
        => _templates.Setup(t => t.GetTrackedByIdAsync(template.Id)).ReturnsAsync(template);

    private void NothingVisible()
        => _templates.Setup(t => t.GetTrackedByIdAsync(It.IsAny<Guid>())).ReturnsAsync((RecurringTransaction?)null);

    private static RecurringTransaction Template(
        RecurringTransactionStatus status,
        DateTime? start = null,
        DateTime? next = null,
        DateTime? end = null)
        => RecurringTemplateFactory.Template(
            status,
            start ?? DateTime.UtcNow.AddMonths(-6),
            next ?? DateTime.UtcNow.AddMonths(1),
            end);

    // ---- Pause ---------------------------------------------------------------------

    [Fact]
    public async Task Pause_AnActiveTemplate_BecomesPausedAndKeepsItsSchedule()
    {
        var scheduled = DateTime.UtcNow.AddDays(3);
        var template = Template(RecurringTransactionStatus.Active, next: scheduled);
        Existing(template);

        var result = await new PauseRecurringTransactionCommandHandler(_templates.Object)
            .Handle(new PauseRecurringTransactionCommand(template.Id), CancellationToken.None);

        result.Outcome.Should().Be(RecurringTransactionOutcome.Success);
        template.Status.Should().Be(RecurringTransactionStatus.Paused);
        template.NextOccurrenceDate.Should().Be(scheduled, "pausing decides nothing about the schedule; resuming does");
        _templates.Verify(t => t.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task Pause_AnAlreadyPausedTemplate_SucceedsWithoutWriting()
    {
        var template = Template(RecurringTransactionStatus.Paused);
        Existing(template);

        var result = await new PauseRecurringTransactionCommandHandler(_templates.Object)
            .Handle(new PauseRecurringTransactionCommand(template.Id), CancellationToken.None);

        result.Outcome.Should().Be(RecurringTransactionOutcome.Success);
        _templates.Verify(t => t.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task Pause_ACancelledTemplate_IsAConflict()
    {
        var template = Template(RecurringTransactionStatus.Cancelled);
        Existing(template);

        var result = await new PauseRecurringTransactionCommandHandler(_templates.Object)
            .Handle(new PauseRecurringTransactionCommand(template.Id), CancellationToken.None);

        result.Outcome.Should().Be(RecurringTransactionOutcome.Conflict);
        template.Status.Should().Be(RecurringTransactionStatus.Cancelled);
    }

    [Fact]
    public async Task Pause_SomethingTheCallerCannotSee_IsNotFound()
    {
        NothingVisible();

        var result = await new PauseRecurringTransactionCommandHandler(_templates.Object)
            .Handle(new PauseRecurringTransactionCommand(Guid.NewGuid()), CancellationToken.None);

        result.Outcome.Should().Be(RecurringTransactionOutcome.NotFound);
    }

    // ---- Resume --------------------------------------------------------------------

    [Fact]
    public async Task Resume_AfterTheScheduleWentStale_FastForwardsInsteadOfBackfilling()
    {
        // Paused in January, resumed now: five missed occurrences. Left alone, the worker's
        // catch-up loop would write all five on its next run.
        var start = new DateTime(DateTime.UtcNow.Year, 1, 10, 0, 0, 0, DateTimeKind.Utc);
        var template = Template(RecurringTransactionStatus.Paused, start: start, next: start);
        Existing(template);

        var result = await new ResumeRecurringTransactionCommandHandler(_templates.Object)
            .Handle(new ResumeRecurringTransactionCommand(template.Id), CancellationToken.None);

        result.Outcome.Should().Be(RecurringTransactionOutcome.Success);
        template.Status.Should().Be(RecurringTransactionStatus.Active);
        template.NextOccurrenceDate.Should().BeOnOrAfter(DateTime.UtcNow.Date,
            "the pause said those occurrences should not be recorded");
        template.NextOccurrenceDate.Day.Should().Be(10, "the walk stays anchored on StartDate");
        _templates.Verify(t => t.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task Resume_WhenTheScheduleIsStillInTheFuture_LeavesTheDateAlone()
    {
        var scheduled = DateTime.UtcNow.AddDays(5);
        var template = Template(RecurringTransactionStatus.Paused, next: scheduled);
        Existing(template);

        var result = await new ResumeRecurringTransactionCommandHandler(_templates.Object)
            .Handle(new ResumeRecurringTransactionCommand(template.Id), CancellationToken.None);

        result.Outcome.Should().Be(RecurringTransactionOutcome.Success);
        template.NextOccurrenceDate.Should().Be(scheduled, "nothing was missed, so nothing should be skipped");
    }

    [Fact]
    public async Task Resume_AnAlreadyActiveTemplate_SucceedsWithoutTouchingADueOccurrence()
    {
        // The dangerous no-op: fast-forwarding here would skip an occurrence that is due now.
        var due = DateTime.UtcNow.AddDays(-1);
        var template = Template(RecurringTransactionStatus.Active, next: due);
        Existing(template);

        var result = await new ResumeRecurringTransactionCommandHandler(_templates.Object)
            .Handle(new ResumeRecurringTransactionCommand(template.Id), CancellationToken.None);

        result.Outcome.Should().Be(RecurringTransactionOutcome.Success);
        template.NextOccurrenceDate.Should().Be(due);
        _templates.Verify(t => t.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task Resume_ACancelledTemplate_IsAConflict()
    {
        var template = Template(RecurringTransactionStatus.Cancelled);
        Existing(template);

        var result = await new ResumeRecurringTransactionCommandHandler(_templates.Object)
            .Handle(new ResumeRecurringTransactionCommand(template.Id), CancellationToken.None);

        result.Outcome.Should().Be(RecurringTransactionOutcome.Conflict);
        result.Message.Should().Contain("cancelled");
        template.Status.Should().Be(RecurringTransactionStatus.Cancelled);
    }

    [Fact]
    public async Task Resume_WhenNoOccurrenceRemainsBeforeTheEndDate_IsAConflict()
    {
        var start = DateTime.UtcNow.AddMonths(-6);
        var template = Template(
            RecurringTransactionStatus.Paused,
            start: start,
            next: start,
            end: DateTime.UtcNow.AddMonths(-1));
        Existing(template);

        var result = await new ResumeRecurringTransactionCommandHandler(_templates.Object)
            .Handle(new ResumeRecurringTransactionCommand(template.Id), CancellationToken.None);

        result.Outcome.Should().Be(RecurringTransactionOutcome.Conflict);
        template.Status.Should().Be(RecurringTransactionStatus.Paused, "an inert Active template reads as broken");
        _templates.Verify(t => t.SaveChangesAsync(), Times.Never);
    }

    // ---- Cancel --------------------------------------------------------------------

    [Theory]
    [InlineData(RecurringTransactionStatus.Active)]
    [InlineData(RecurringTransactionStatus.Paused)]
    public async Task Cancel_FromEitherLiveState_Succeeds(RecurringTransactionStatus from)
    {
        var template = Template(from);
        Existing(template);

        var result = await new CancelRecurringTransactionCommandHandler(_templates.Object)
            .Handle(new CancelRecurringTransactionCommand(template.Id), CancellationToken.None);

        result.Outcome.Should().Be(RecurringTransactionOutcome.Success);
        template.Status.Should().Be(RecurringTransactionStatus.Cancelled);
        _templates.Verify(t => t.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task Cancel_AnAlreadyCancelledTemplate_SucceedsWithoutWriting()
    {
        var template = Template(RecurringTransactionStatus.Cancelled);
        Existing(template);

        var result = await new CancelRecurringTransactionCommandHandler(_templates.Object)
            .Handle(new CancelRecurringTransactionCommand(template.Id), CancellationToken.None);

        result.Outcome.Should().Be(RecurringTransactionOutcome.Success);
        _templates.Verify(t => t.SaveChangesAsync(), Times.Never);
    }

    // ---- Delete --------------------------------------------------------------------

    [Fact]
    public async Task Delete_ATemplateThatHasGeneratedNothing_RemovesIt()
    {
        var template = Template(RecurringTransactionStatus.Active);
        Existing(template);
        _templates.Setup(t => t.CountGeneratedTransactionsAsync(template.Id)).ReturnsAsync(0);

        var result = await new DeleteRecurringTransactionCommandHandler(_templates.Object)
            .Handle(new DeleteRecurringTransactionCommand(template.Id), CancellationToken.None);

        result.Outcome.Should().Be(RecurringTransactionOutcome.Success);
        _templates.Verify(t => t.DeleteAsync(template), Times.Once);
    }

    [Fact]
    public async Task Delete_ATemplateWithGeneratedTransactions_IsRefusedSoTheHistorySurvives()
    {
        // The FK is OnDelete(SetNull): deleting would leave those rows in place with no
        // record of where they came from, and nothing in the UI would show it happened.
        var template = Template(RecurringTransactionStatus.Active);
        Existing(template);
        _templates.Setup(t => t.CountGeneratedTransactionsAsync(template.Id)).ReturnsAsync(7);

        var result = await new DeleteRecurringTransactionCommandHandler(_templates.Object)
            .Handle(new DeleteRecurringTransactionCommand(template.Id), CancellationToken.None);

        result.Outcome.Should().Be(RecurringTransactionOutcome.Conflict);
        result.Message.Should().Contain("7").And.Contain("Cancel");
        _templates.Verify(t => t.DeleteAsync(It.IsAny<RecurringTransaction>()), Times.Never);
    }

    [Fact]
    public async Task Delete_SomethingTheCallerCannotSee_IsNotFoundAndDeletesNothing()
    {
        NothingVisible();

        var result = await new DeleteRecurringTransactionCommandHandler(_templates.Object)
            .Handle(new DeleteRecurringTransactionCommand(Guid.NewGuid()), CancellationToken.None);

        result.Outcome.Should().Be(RecurringTransactionOutcome.NotFound);
        _templates.Verify(t => t.DeleteAsync(It.IsAny<RecurringTransaction>()), Times.Never);
    }

    // ---- Update --------------------------------------------------------------------

    private UpdateRecurringTransactionCommandHandler UpdateSut(RecurringTransaction template)
    {
        _categories.Setup(c => c.GetByIdAsync(template.CategoryId)).ReturnsAsync(template.Category);
        _frequencies.Setup(f => f.GetByIdAsync(template.FrequencyId)).ReturnsAsync(template.Frequency);
        _templates.Setup(t => t.GetByIdAsync(template.Id)).ReturnsAsync(template);
        return new UpdateRecurringTransactionCommandHandler(_templates.Object, _categories.Object, _frequencies.Object);
    }

    private static UpdateRecurringTransactionDto DtoFrom(RecurringTransaction template) => new()
    {
        Name = template.Name,
        Description = template.Description,
        Amount = template.DefaultAmount,
        CategoryId = template.CategoryId,
        FrequencyId = template.FrequencyId,
        StartDate = template.StartDate,
        EndDate = template.EndDate
    };

    [Fact]
    public async Task Update_ThatLeavesTheScheduleAlone_DoesNotMoveADueOccurrence()
    {
        // Renaming a template whose occurrence is due today must not push that occurrence
        // into the future and skip it.
        var due = DateTime.UtcNow.AddHours(-1);
        var template = Template(RecurringTransactionStatus.Active, next: due);
        Existing(template);

        var dto = DtoFrom(template);
        dto.Name = "Rent (renamed)";
        dto.Amount = 1500m;

        var result = await UpdateSut(template)
            .Handle(new UpdateRecurringTransactionCommand(template.Id, dto), CancellationToken.None);

        result.Outcome.Should().Be(RecurringTransactionOutcome.Success);
        template.Name.Should().Be("Rent (renamed)");
        template.DefaultAmount.Should().Be(1500m);
        template.NextOccurrenceDate.Should().Be(due);
    }

    [Fact]
    public async Task Update_ThatMovesTheStartDate_ReAnchorsTheSchedule()
    {
        var template = Template(RecurringTransactionStatus.Active, next: DateTime.UtcNow.AddDays(2));
        Existing(template);

        var newStart = DateTime.UtcNow.AddDays(20);
        var dto = DtoFrom(template);
        dto.StartDate = newStart;

        var result = await UpdateSut(template)
            .Handle(new UpdateRecurringTransactionCommand(template.Id, dto), CancellationToken.None);

        result.Outcome.Should().Be(RecurringTransactionOutcome.Success);
        template.NextOccurrenceDate.Should().Be(newStart, "the anchor moved, so the derived schedule has to move with it");
    }

    [Fact]
    public async Task Update_ACancelledTemplate_IsAConflict()
    {
        var template = Template(RecurringTransactionStatus.Cancelled);
        Existing(template);

        var dto = DtoFrom(template);
        dto.Name = "Rewriting history";

        var result = await UpdateSut(template)
            .Handle(new UpdateRecurringTransactionCommand(template.Id, dto), CancellationToken.None);

        result.Outcome.Should().Be(RecurringTransactionOutcome.Conflict);
        template.Name.Should().Be("Rent");
        _templates.Verify(t => t.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task Update_SomethingTheCallerCannotSee_IsNotFound()
    {
        NothingVisible();

        var template = Template(RecurringTransactionStatus.Active);
        var handler = new UpdateRecurringTransactionCommandHandler(
            _templates.Object, _categories.Object, _frequencies.Object);

        var result = await handler.Handle(
            new UpdateRecurringTransactionCommand(Guid.NewGuid(), DtoFrom(template)), CancellationToken.None);

        result.Outcome.Should().Be(RecurringTransactionOutcome.NotFound);
        _templates.Verify(t => t.SaveChangesAsync(), Times.Never);
    }
}
