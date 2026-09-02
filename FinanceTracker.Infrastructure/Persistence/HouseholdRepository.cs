using FinanceTracker.Domain.Entities;
using FinanceTracker.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Persistence;

public class HouseholdRepository : IHouseholdRepository
{
    private readonly FinanceTrackerContext _context;

    public HouseholdRepository(FinanceTrackerContext context) => _context = context;

    public Task<Household?> GetByIdAsync(Guid householdId, CancellationToken cancellationToken = default) =>
        _context.Households
            .Include(h => h.Members)
            .SingleOrDefaultAsync(h => h.Id == householdId, cancellationToken);

    public async Task<Household?> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var householdId = await GetHouseholdIdForUserAsync(userId, cancellationToken);

        return householdId is null ? null : await GetByIdAsync(householdId.Value, cancellationToken);
    }

    public Task<Guid?> GetHouseholdIdForUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _context.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.HouseholdId)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task AddAsync(Household household, CancellationToken cancellationToken = default) =>
        await _context.Households.AddAsync(household, cancellationToken);

    public Task RemoveAsync(Household household, CancellationToken cancellationToken = default)
    {
        _context.Households.Remove(household);
        return Task.CompletedTask;
    }

    public Task<List<User>> GetMembersAsync(Guid householdId, CancellationToken cancellationToken = default) =>
        _context.Users
            .Where(u => u.HouseholdId == householdId)
            .OrderBy(u => u.Email)
            .ToListAsync(cancellationToken);

    public async Task AddInvitationAsync(
        HouseholdInvitation invitation,
        CancellationToken cancellationToken = default) =>
        await _context.HouseholdInvitations.AddAsync(invitation, cancellationToken);

    public Task<HouseholdInvitation?> GetInvitationAsync(
        Guid invitationId,
        CancellationToken cancellationToken = default) =>
        _context.HouseholdInvitations
            .Include(i => i.Household)
            .SingleOrDefaultAsync(i => i.Id == invitationId, cancellationToken);

    public Task<List<HouseholdInvitation>> GetInvitationsForHouseholdAsync(
        Guid householdId,
        CancellationToken cancellationToken = default) =>
        _context.HouseholdInvitations
            .AsNoTracking()
            .Where(i => i.HouseholdId == householdId)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(cancellationToken);

    public Task<List<HouseholdInvitation>> GetOpenInvitationsForEmailAsync(
        string normalizedEmail,
        DateTime asOf,
        CancellationToken cancellationToken = default) =>
        _context.HouseholdInvitations
            .AsNoTracking()
            .Include(i => i.Household)
            .Where(i => i.InvitedEmail == normalizedEmail
                     && i.Status == HouseholdInvitationStatus.Pending
                     && i.ExpiresAt > asOf)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(cancellationToken);

    public Task<bool> HasOpenInvitationAsync(
        Guid householdId,
        string normalizedEmail,
        DateTime asOf,
        CancellationToken cancellationToken = default) =>
        _context.HouseholdInvitations
            .AnyAsync(
                i => i.HouseholdId == householdId
                  && i.InvitedEmail == normalizedEmail
                  && i.Status == HouseholdInvitationStatus.Pending
                  && i.ExpiresAt > asOf,
                cancellationToken);

    public Task StampRecordsAsync(
        Guid userId,
        Guid householdId,
        CancellationToken cancellationToken = default) =>
        MoveRecordsAsync(userId, householdId, cancellationToken);

    public Task DetachRecordsAsync(Guid userId, CancellationToken cancellationToken = default) =>
        MoveRecordsAsync(userId, null, cancellationToken);

    /// <summary>
    /// Moves one person's records into a household, or out of whatever they are in.
    ///
    /// IgnoreQueryFilters throughout: the caller is mid-transition, so the filter would hide
    /// the very rows this exists to move.
    /// </summary>
    private async Task MoveRecordsAsync(
        Guid userId,
        Guid? householdId,
        CancellationToken cancellationToken)
    {
        var transactions = await _context.Transactions
            .IgnoreQueryFilters()
            .Where(t => t.UserId == userId)
            .ToListAsync(cancellationToken);

        foreach (var transaction in transactions)
            transaction.HouseholdId = householdId;

        var templates = await _context.RecurringTransactions
            .IgnoreQueryFilters()
            .Where(r => r.UserId == userId)
            .ToListAsync(cancellationToken);

        foreach (var template in templates)
            template.HouseholdId = householdId;

        // Read before the categories are touched, and from the database rather than the
        // change tracker. The rows written above belong to this user and are excluded by
        // owner anyway.
        var pinned = await CategoryIdsPinnedByOthersAsync(userId, cancellationToken);

        var categories = await _context.Categories
            .IgnoreQueryFilters()
            .Where(c => c.UserId == userId)
            .ToListAsync(cancellationToken);

        foreach (var category in categories)
        {
            // A pinned category does not move — in either direction. See
            // CategoryIdsPinnedByOthersAsync.
            if (!pinned.Contains(category.Id))
                category.HouseholdId = householdId;
        }
    }

    public async Task ClearHouseholdStampAsync(
        Guid householdId,
        CancellationToken cancellationToken = default)
    {
        var categories = await _context.Categories
            .IgnoreQueryFilters()
            .Where(c => c.HouseholdId == householdId)
            .ToListAsync(cancellationToken);

        foreach (var category in categories)
            category.HouseholdId = null;

        var transactions = await _context.Transactions
            .IgnoreQueryFilters()
            .Where(t => t.HouseholdId == householdId)
            .ToListAsync(cancellationToken);

        foreach (var transaction in transactions)
            transaction.HouseholdId = null;

        var templates = await _context.RecurringTransactions
            .IgnoreQueryFilters()
            .Where(r => r.HouseholdId == householdId)
            .ToListAsync(cancellationToken);

        foreach (var template in templates)
            template.HouseholdId = null;
    }

    /// <summary>
    /// This user's categories that somebody else's records depend on.
    ///
    /// A <c>Transaction</c>'s category is a *required* navigation, so moving a category out
    /// of the scope where another person's transaction can see it takes that transaction out
    /// of its own owner's list — the filter hides the principal and the required join drops
    /// the dependent. Such a category is pinned where it is, in **both** directions: leaving
    /// a household must not strand the people still in it, and joining a new one must not
    /// drag a category out of the household that is still using it.
    ///
    /// Nothing is lost by pinning. The owner keeps seeing their category through the
    /// ownership arm of the filter wherever they go.
    /// </summary>
    private async Task<HashSet<Guid>> CategoryIdsPinnedByOthersAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var owned = await _context.Categories
            .IgnoreQueryFilters()
            .Where(c => c.UserId == userId)
            .Select(c => c.Id)
            .ToListAsync(cancellationToken);

        if (owned.Count == 0)
            return [];

        // Two queries rather than one Union: the InMemory provider the tests run on is the
        // weakest link in the translation chain, and this costs nothing at these volumes.
        var fromTransactions = await _context.Transactions
            .IgnoreQueryFilters()
            .Where(t => t.UserId != userId && owned.Contains(t.CategoryId))
            .Select(t => t.CategoryId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var fromTemplates = await _context.RecurringTransactions
            .IgnoreQueryFilters()
            .Where(r => r.UserId != userId && owned.Contains(r.CategoryId))
            .Select(r => r.CategoryId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return fromTransactions.Concat(fromTemplates).ToHashSet();
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _context.SaveChangesAsync(cancellationToken);
}
