namespace FinanceTracker.Domain.Services;

/// <summary>
/// The tenant the current operation belongs to.
///
/// Lives in Domain because both Application and Infrastructure need it and Domain depends
/// on nothing: the handlers read it to stamp an owner on new records, and the DbContext
/// reads it to scope every query.
///
/// <c>UserId</c> is <c>required</c> on every financial entity, so the compiler refuses any
/// write that does not name an owner, and this is where that owner comes from.
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

    /// <summary>
    /// The signed-in user's email, taken from the token rather than the database so that
    /// stamping an audit label costs no round trip. Null when there is no user context.
    /// </summary>
    string? Email => null;

    /// <summary>
    /// The household the signed-in user shares their finances with, or null when they are
    /// on their own — which is also what the worker and any unauthenticated path report.
    ///
    /// Resolved per request from the database rather than from a token claim: a claim minted
    /// at sign-in would still say "no household" for the life of the access token after
    /// someone accepted an invitation, and they would sit looking at an empty shared view
    /// with nothing to explain it.
    ///
    /// Defaulted here so an accessor that predates households — the worker's, the tests' —
    /// keeps compiling and keeps meaning "no household".
    /// </summary>
    Guid? HouseholdId => null;
}
