namespace FinanceTracker.Worker.Services;

/// <summary>
/// A mutual-exclusion lock held for the duration of one generation run, so a scheduled
/// invocation firing while a previous slow run is still going cannot double-generate
/// transactions for the same overdue template.
///
/// Behind an interface because the SQL Server implementation needs a live relational
/// connection, which the InMemory provider used by the test suite cannot supply — and
/// because the acquire/release contract is worth asserting on directly.
/// </summary>
public interface IRunLock
{
    /// <summary>
    /// Attempts to take the lock. Returns <c>false</c> when another run already holds it,
    /// in which case the caller must skip its run and must not call
    /// <see cref="ReleaseAsync"/> — releasing a lock this process does not own is an error.
    /// </summary>
    Task<bool> TryAcquireAsync();

    /// <summary>Releases a lock previously taken by <see cref="TryAcquireAsync"/>.</summary>
    Task ReleaseAsync();
}
