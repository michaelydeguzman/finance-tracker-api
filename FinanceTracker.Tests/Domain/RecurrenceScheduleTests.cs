using FinanceTracker.Domain.Entities;
using FinanceTracker.Domain.Services;
using FluentAssertions;

namespace FinanceTracker.Tests.Domain;

/// <summary>
/// The fast-forward that both creating and resuming a template rely on. Its job is to reach
/// the first occurrence still due <em>without</em> reimplementing any date arithmetic, so
/// most of these assert that the calculator's behaviour survives the walk.
/// </summary>
public class RecurrenceScheduleTests
{
    private static DateTime Utc(string value)
        => DateTime.Parse(value, null, System.Globalization.DateTimeStyles.RoundtripKind);

    [Fact]
    public void FirstDueOnOrAfter_WhenTheScheduleIsAlreadyInTheFuture_LeavesItAlone()
    {
        var start = Utc("2026-09-01T00:00:00Z");

        var result = RecurrenceSchedule.FirstDueOnOrAfter(
            FrequencyType.Monthly, null, start, start, asOf: Utc("2026-08-26T00:00:00Z"));

        result.Should().Be(start, "a template that has not started yet must not be advanced past its own start date");
    }

    [Fact]
    public void FirstDueOnOrAfter_WhenTheOccurrenceIsExactlyNow_LeavesItAlone()
    {
        var now = Utc("2026-08-26T09:00:00Z");

        var result = RecurrenceSchedule.FirstDueOnOrAfter(FrequencyType.Daily, null, now, now, asOf: now);

        result.Should().Be(now, "an occurrence due this instant is still due, not missed");
    }

    [Fact]
    public void FirstDueOnOrAfter_FromAPastStartDate_ReturnsOneFutureOccurrence_NotABacklog()
    {
        // A monthly template anchored five months ago. The point of the whole helper: the
        // result is a single date in the future, so the worker generates one occurrence when
        // it next comes round rather than five at once.
        var start = Utc("2026-01-10T00:00:00Z");

        var result = RecurrenceSchedule.FirstDueOnOrAfter(
            FrequencyType.Monthly, null, start, start, asOf: Utc("2026-06-15T00:00:00Z"));

        result.Should().Be(Utc("2026-07-10T00:00:00Z"));
    }

    [Fact]
    public void FirstDueOnOrAfter_PreservesTheSnapBackAnchor()
    {
        // The invariant RecurrenceCalculator exists to protect: targetDay comes from
        // startDate.Day, never from the date being advanced. Anchoring on the current date
        // instead would clamp to Feb 28 and then stay on the 28th for ever — this would
        // return 2026-03-28.
        var start = Utc("2026-01-31T00:00:00Z");

        var result = RecurrenceSchedule.FirstDueOnOrAfter(
            FrequencyType.Monthly, null, start, start, asOf: Utc("2026-03-15T00:00:00Z"));

        result.Should().Be(Utc("2026-03-31T00:00:00Z"), "February must not permanently drag the schedule off the 31st");
    }

    [Fact]
    public void FirstDueOnOrAfter_WalksFromTheScheduledDate_NotOnlyFromTheStartDate()
    {
        // What resuming needs: the template started in January but its schedule had already
        // been advanced to April before it was paused.
        var start = Utc("2026-01-10T00:00:00Z");

        var result = RecurrenceSchedule.FirstDueOnOrAfter(
            FrequencyType.Monthly, null, start, Utc("2026-04-10T00:00:00Z"), asOf: Utc("2026-06-15T00:00:00Z"));

        result.Should().Be(Utc("2026-07-10T00:00:00Z"));
    }

    [Theory]
    [InlineData(FrequencyType.Daily, null, "2026-08-20T00:00:00Z", "2026-08-26T12:00:00Z", "2026-08-27T00:00:00Z")]
    [InlineData(FrequencyType.Weekly, null, "2026-08-03T00:00:00Z", "2026-08-26T00:00:00Z", "2026-08-31T00:00:00Z")]
    [InlineData(FrequencyType.BiWeekly, null, "2026-08-03T00:00:00Z", "2026-08-26T00:00:00Z", "2026-08-31T00:00:00Z")]
    [InlineData(FrequencyType.Quarterly, null, "2026-01-15T00:00:00Z", "2026-08-26T00:00:00Z", "2026-10-15T00:00:00Z")]
    [InlineData(FrequencyType.Annually, null, "2020-02-29T00:00:00Z", "2026-08-26T00:00:00Z", "2027-02-28T00:00:00Z")]
    [InlineData(FrequencyType.Custom, 10, "2026-08-01T00:00:00Z", "2026-08-26T00:00:00Z", "2026-08-31T00:00:00Z")]
    public void FirstDueOnOrAfter_AcrossFrequencies_LandsOnTheFirstOccurrenceAtOrAfterNow(
        FrequencyType type, int? intervalDays, string startStr, string asOfStr, string expectedStr)
    {
        var start = Utc(startStr);

        var result = RecurrenceSchedule.FirstDueOnOrAfter(type, intervalDays, start, start, Utc(asOfStr));

        result.Should().Be(Utc(expectedStr));
        result.Should().BeOnOrAfter(Utc(asOfStr));
    }

    [Fact]
    public void FirstDueOnOrAfter_WithACustomFrequencyThatHasNoInterval_Throws()
    {
        // Straight through from RecurrenceCalculator — the helper must not paper over it.
        var start = Utc("2026-01-01T00:00:00Z");

        var act = () => RecurrenceSchedule.FirstDueOnOrAfter(
            FrequencyType.Custom, null, start, start, asOf: Utc("2026-08-26T00:00:00Z"));

        act.Should().Throw<ArgumentException>()
            .WithMessage("*must be a positive number of days*");
    }
}
