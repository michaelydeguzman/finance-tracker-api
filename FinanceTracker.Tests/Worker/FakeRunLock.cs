using FinanceTracker.Worker.Services;

namespace FinanceTracker.Tests.Worker;

/// <summary>
/// In-memory <see cref="IRunLock"/> for the worker tests. Construct with
/// <c>canAcquire: false</c> to simulate a run that is already in progress.
/// </summary>
public sealed class FakeRunLock : IRunLock
{
    private readonly bool _canAcquire;

    public FakeRunLock(bool canAcquire = true) => _canAcquire = canAcquire;

    public bool AcquireAttempted { get; private set; }

    public bool Released { get; private set; }

    public Task<bool> TryAcquireAsync(CancellationToken cancellationToken = default)
    {
        AcquireAttempted = true;
        return Task.FromResult(_canAcquire);
    }

    public Task ReleaseAsync()
    {
        Released = true;
        return Task.CompletedTask;
    }
}
