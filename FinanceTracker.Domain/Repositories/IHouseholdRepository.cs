using FinanceTracker.Domain.Entities;

namespace FinanceTracker.Domain.Repositories;

/// <summary>
/// Households and their invitations.
///
/// Everything here reads across the tenancy filter by necessity: a household spans users,
/// and the whole point of an invitation is that it is written by one person and answered by
/// another. Membership is therefore checked explicitly in each method's caller rather than
/// leaned on the model-level filter — which is why every read below takes the ids it is
/// allowed to see rather than trusting an id handed in from a request.
/// </summary>
public interface IHouseholdRepository
{
    Task<Household?> GetByIdAsync(Guid householdId, CancellationToken cancellationToken = default);

    /// <summary>The household the user belongs to, with its members loaded, or null.</summary>
    Task<Household?> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Just the membership, for the per-request tenancy lookup. Kept separate from
    /// <see cref="GetForUserAsync"/> so the hot path costs one scalar rather than a
    /// household and every member it has.
    /// </summary>
    Task<Guid?> GetHouseholdIdForUserAsync(Guid userId, CancellationToken cancellationToken = default);

    Task AddAsync(Household household, CancellationToken cancellationToken = default);

    Task RemoveAsync(Household household, CancellationToken cancellationToken = default);

    Task<List<User>> GetMembersAsync(Guid householdId, CancellationToken cancellationToken = default);

    Task AddInvitationAsync(HouseholdInvitation invitation, CancellationToken cancellationToken = default);

    Task<HouseholdInvitation?> GetInvitationAsync(Guid invitationId, CancellationToken cancellationToken = default);

    /// <summary>Every invitation a household has issued, newest first, whatever its status.</summary>
    Task<List<HouseholdInvitation>> GetInvitationsForHouseholdAsync(
        Guid householdId,
        CancellationToken cancellationToken = default);

    /// <summary>Open invitations addressed to a normalized email, with their household loaded.</summary>
    Task<List<HouseholdInvitation>> GetOpenInvitationsForEmailAsync(
        string normalizedEmail,
        DateTime asOf,
        CancellationToken cancellationToken = default);

    /// <summary>Whether this household already has an unanswered offer out to that address.</summary>
    Task<bool> HasOpenInvitationAsync(
        Guid householdId,
        string normalizedEmail,
        DateTime asOf,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stamps every financial record owned by <paramref name="userId"/> with
    /// <paramref name="householdId"/>. The joining direction.
    ///
    /// This is what makes the widened query filter a scalar compare instead of a subquery
    /// over the membership table, and it is what brings a joiner's existing history into the
    /// household rather than starting them from empty.
    ///
    /// Deliberately loads and mutates tracked entities rather than issuing a set-based
    /// update. A personal-finance history is small, and the tests run on the InMemory
    /// provider, which has no <c>ExecuteUpdate</c>.
    /// </summary>
    Task StampRecordsAsync(Guid userId, Guid householdId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Takes <paramref name="userId"/>'s records back out of <paramref name="householdId"/>.
    /// The leaving direction, and deliberately not a mirror image of
    /// <see cref="StampRecordsAsync"/>.
    ///
    /// Transactions and templates leave with their owner. Categories may not: a
    /// <c>Transaction</c>'s category is a *required* navigation, so a category that left
    /// while another member's transaction still pointed at it would take that transaction
    /// out of its own owner's list — the filter hides the principal and the required join
    /// drops the dependent. Categories another member still references therefore stay with
    /// the household. Nothing is lost by that: their owner keeps seeing them through the
    /// ownership arm of the filter, exactly as they would have.
    /// </summary>
    Task DetachRecordsAsync(Guid userId, Guid householdId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears the household stamp from every record carrying it, whoever owns them.
    ///
    /// Called immediately before deleting a household. Every tenancy FK is <c>Restrict</c>,
    /// so a single row left pointing at it — a category kept behind by
    /// <see cref="DetachRecordsAsync"/>, or one stamped by a write that raced a removal —
    /// turns the delete into a <c>DbUpdateException</c> and leaves the household
    /// permanently uncloseable.
    /// </summary>
    Task ClearHouseholdStampAsync(Guid householdId, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
