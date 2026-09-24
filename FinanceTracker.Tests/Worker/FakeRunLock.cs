using FinanceTracker.Worker.Services;

namespace FinanceTracker.Tests.Worker;

/// <summary>
/// In-memory <see cref="IRunLock"/> for the worker tests. Construct with
/// <c>canAcquire: false</c> to simulate a run that is already in progress, or set
/// <see cref="HeldChecksBeforeLoss"/> to simulate the lock being lost mid-run.
/// </summary>
public sealed class FakeRunLock : IRunLock
{
    private readonly bool _canAcquire;

    public FakeRunLock(bool canAcquire = true) => _canAcquire = canAcquire;

    public bool AcquireAttempted { get; private set; }

    public bool Released { get; private set; }

    /// <summary>
    /// How many <see cref="IsHeldAsync"/> calls answer true before the lock reads as lost —
    /// what a dropped connection does to a session-scoped lock.
    /// </summary>
    public int HeldChecksBeforeLoss { get; init; } = int.MaxValue;

    public int HeldChecks { get; private set; }

    public Task<bool> TryAcquireAsync(CancellationToken cancellationToken = default)
    {
        AcquireAttempted = true;
        return Task.FromResult(_canAcquire);
    }

    public Task<bool> IsHeldAsync(CancellationToken cancellationToken = default)
    {
        HeldChecks++;
        return Task.FromResult(HeldChecks <= HeldChecksBeforeLoss);
    }

    public Task ReleaseAsync()
    {
        Released = true;
        return Task.CompletedTask;
    }
}
