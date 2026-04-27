using FinanceTracker.Domain.Entities;

namespace FinanceTracker.Domain.Services;

public static class RecurrenceCalculator
{
    public static DateTime NextOccurrence(
        FrequencyType type,
        int? intervalDays,
        DateTime currentDate,
        DateTime startDate)
        => throw new NotImplementedException();
}
