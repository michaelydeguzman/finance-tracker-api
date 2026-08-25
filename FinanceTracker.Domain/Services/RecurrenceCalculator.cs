using FinanceTracker.Domain.Entities;

namespace FinanceTracker.Domain.Services;

public static class RecurrenceCalculator
{
    public static DateTime NextOccurrence(
        FrequencyType type,
        int? intervalDays,
        DateTime currentDate,
        DateTime startDate)
    {
        int targetDay = startDate.Day;

        return type switch
        {
            FrequencyType.Daily        => currentDate.AddDays(1),
            FrequencyType.Weekly       => currentDate.AddDays(7),
            FrequencyType.BiWeekly     => currentDate.AddDays(14),
            FrequencyType.Monthly      => AddMonthsWithSnapBack(currentDate, targetDay, 1),
            FrequencyType.Quarterly    => AddMonthsWithSnapBack(currentDate, targetDay, 3),
            FrequencyType.SemiAnnually => AddMonthsWithSnapBack(currentDate, targetDay, 6),
            FrequencyType.Annually     => AddMonthsWithSnapBack(currentDate, targetDay, 12),
            FrequencyType.Custom       => currentDate.AddDays(
                                             intervalDays is > 0
                                                 ? intervalDays.Value
                                                 : throw new ArgumentException(
                                                     "IntervalDays must be a positive number of days for Custom frequency.",
                                                     nameof(intervalDays))),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unhandled FrequencyType.")
        };
    }

    // AddMonths handles year/month rollover and leap-year month counts correctly.
    // We override only the day component to prevent permanent drift after short-month clamping.
    // targetDay must always come from startDate.Day — never currentDate.Day (D-03).
    private static DateTime AddMonthsWithSnapBack(DateTime currentDate, int targetDay, int months)
    {
        DateTime shifted = currentDate.AddMonths(months);
        int clampedDay = Math.Min(targetDay, DateTime.DaysInMonth(shifted.Year, shifted.Month));
        return new DateTime(shifted.Year, shifted.Month, clampedDay,
                            currentDate.Hour, currentDate.Minute, currentDate.Second,
                            currentDate.Kind);
    }
}
