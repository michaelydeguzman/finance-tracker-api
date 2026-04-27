using FinanceTracker.Domain.Entities;
using FinanceTracker.Domain.Services;
using FluentAssertions;

namespace FinanceTracker.Tests.Domain;

public class RecurrenceCalculatorTests
{
    [Theory]
    [InlineData(FrequencyType.Daily,        null, "2026-01-15", "2026-01-01", "2026-01-16")]
    [InlineData(FrequencyType.Weekly,       null, "2026-01-15", "2026-01-01", "2026-01-22")]
    [InlineData(FrequencyType.BiWeekly,     null, "2026-01-15", "2026-01-01", "2026-01-29")]
    [InlineData(FrequencyType.Monthly,      null, "2026-01-15", "2026-01-15", "2026-02-15")]
    [InlineData(FrequencyType.Quarterly,    null, "2026-01-15", "2026-01-15", "2026-04-15")]
    [InlineData(FrequencyType.SemiAnnually, null, "2026-01-15", "2026-01-15", "2026-07-15")]
    [InlineData(FrequencyType.Annually,     null, "2026-01-15", "2026-01-15", "2027-01-15")]
    [InlineData(FrequencyType.Custom,       30,   "2026-01-15", "2026-01-15", "2026-02-14")]
    public void NextOccurrence_HappyPath_ReturnsExpectedDate(
        FrequencyType type, int? intervalDays,
        string currentStr, string startStr, string expectedStr)
    {
        var current  = DateTime.Parse(currentStr,  null, System.Globalization.DateTimeStyles.RoundtripKind);
        var start    = DateTime.Parse(startStr,    null, System.Globalization.DateTimeStyles.RoundtripKind);
        var expected = DateTime.Parse(expectedStr, null, System.Globalization.DateTimeStyles.RoundtripKind);

        var result = RecurrenceCalculator.NextOccurrence(type, intervalDays, current, start);

        result.Should().Be(expected);
    }

    [Fact]
    public void NextOccurrence_Monthly_SnapBackAfterFebruary_ReturnsMarch31()
    {
        var start   = new DateTime(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc);
        var current = new DateTime(2026, 2, 28, 0, 0, 0, DateTimeKind.Utc);
        var result  = RecurrenceCalculator.NextOccurrence(FrequencyType.Monthly, null, current, start);
        result.Should().Be(new DateTime(2026, 3, 31, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void NextOccurrence_Monthly_YearEndRollover_ReturnsJan31()
    {
        var start   = new DateTime(2025, 12, 31, 0, 0, 0, DateTimeKind.Utc);
        var current = new DateTime(2025, 12, 31, 0, 0, 0, DateTimeKind.Utc);
        var result  = RecurrenceCalculator.NextOccurrence(FrequencyType.Monthly, null, current, start);
        result.Should().Be(new DateTime(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void NextOccurrence_Monthly_StartFeb29LeapYear_ClampsToMar29()
    {
        var start   = new DateTime(2024, 2, 29, 0, 0, 0, DateTimeKind.Utc);
        var current = new DateTime(2024, 2, 29, 0, 0, 0, DateTimeKind.Utc);
        var result  = RecurrenceCalculator.NextOccurrence(FrequencyType.Monthly, null, current, start);
        result.Should().Be(new DateTime(2024, 3, 29, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void NextOccurrence_Custom_NullIntervalDays_ThrowsArgumentException()
    {
        var start   = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc);
        var current = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc);
        var act = () => RecurrenceCalculator.NextOccurrence(FrequencyType.Custom, null, current, start);
        act.Should().Throw<ArgumentException>()
           .WithMessage("*IntervalDays must be set for Custom frequency*");
    }
}
