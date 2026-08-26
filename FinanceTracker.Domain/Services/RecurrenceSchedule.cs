using FinanceTracker.Domain.Entities;

namespace FinanceTracker.Domain.Services;

/// <summary>
/// Walks a template's schedule forward to the first occurrence that is still due.
///
/// This composes <see cref="RecurrenceCalculator"/> rather than repeating any of its date
/// maths: every step goes through <see cref="RecurrenceCalculator.NextOccurrence"/> with the
/// template's original <c>startDate</c> as the anchor, so the snap-back invariant (a date
/// clamped by a short month must not drift permanently) holds here exactly as it does in
/// the worker.
///
/// Pure and dependency-free, for the same reason the calculator is: it makes the date logic
/// trivially testable and lets the whole suite run anywhere.
/// </summary>
public static class RecurrenceSchedule
{
    /// <summary>
    /// A template this far behind with an interval this short is pathological rather than
    /// merely stale; failing loudly beats spinning.
    /// </summary>
    private const int MaxAdvanceSteps = 100_000;

    /// <summary>
    /// The first occurrence on or after <paramref name="asOf"/>, walking the schedule from
    /// <paramref name="scheduledDate"/>.
    /// </summary>
    /// <param name="type">The frequency's type.</param>
    /// <param name="intervalDays">Only meaningful for <see cref="FrequencyType.Custom"/>.</param>
    /// <param name="startDate">
    /// The immutable anchor the sequence is derived from. Never substitute the current date:
    /// that is precisely the drift bug the calculator's snap-back exists to prevent.
    /// </param>
    /// <param name="scheduledDate">Where to begin walking — a template's current occurrence date.</param>
    /// <param name="asOf">The floor, normally "now".</param>
    public static DateTime FirstDueOnOrAfter(
        FrequencyType type,
        int? intervalDays,
        DateTime startDate,
        DateTime scheduledDate,
        DateTime asOf)
    {
        var next = scheduledDate;

        for (var step = 0; next < asOf; step++)
        {
            if (step >= MaxAdvanceSteps)
            {
                throw new InvalidOperationException(
                    $"Could not reach {asOf:O} within {MaxAdvanceSteps} occurrences of the schedule starting {startDate:O}.");
            }

            var candidate = RecurrenceCalculator.NextOccurrence(type, intervalDays, next, startDate);

            // A frequency that does not move the date forward would loop for ever. The
            // calculator never returns one today; this refuses to depend on that staying true.
            if (candidate <= next)
            {
                throw new InvalidOperationException(
                    $"Frequency '{type}' did not advance the occurrence date past {next:O}.");
            }

            next = candidate;
        }

        return next;
    }
}
