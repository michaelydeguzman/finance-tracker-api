namespace FinanceTracker.Application.Services;

/// <summary>
/// The tenant the current operation belongs to.
///
/// Introduced with the tenancy columns because <c>UserId</c> is <c>required</c> on every
/// financial entity: the compiler now refuses any write that does not name an owner, and
/// this is where that owner comes from. Phase 2 backs it with JWT claims; until then the
/// API implementation resolves nothing and writes fail closed rather than creating
/// unowned rows.
/// </summary>
public interface ICurrentUserAccessor
{
    /// <summary>
    /// The signed-in user, or <c>null</c> where there is no user context at all — the
    /// worker sweeps every tenant's templates and has no single owner.
    /// </summary>
    Guid? UserId { get; }

    /// <summary>
    /// The signed-in user, or a thrown exception. Use this on any path that writes a
    /// tenancy-scoped record, so a missing identity surfaces as a failed request instead
    /// of an orphaned row.
    /// </summary>
    Guid RequireUserId() => UserId ?? throw new InvalidOperationException(
        "No authenticated user is in scope for this operation.");
}
