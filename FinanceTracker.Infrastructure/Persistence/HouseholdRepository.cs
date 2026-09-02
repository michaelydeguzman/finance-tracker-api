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

    public async Task DetachRecordsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        // Before anything moves. Once this has run, nothing this user owns depends on a
        // category anybody else owns, so moving their rows out cannot separate a transaction
        // from its category.
        await ForkBorrowedCategoriesAsync(userId, cancellationToken);

        await MoveRecordsAsync(userId, null, cancellationToken);
    }

    /// <summary>
    /// Gives this user their own copy of any category their records point at but somebody
    /// else owns, and re-points those records at the copy.
    ///
    /// This is the other half of the required-navigation problem, and the half that costs the
    /// *leaver* rather than the people staying. While sharing, anyone may file a transaction
    /// under any category in the household. On the way out those categories stay behind with
    /// their owner — so without this, a leaver's own transactions point at a category they can
    /// no longer see, the required join drops them, and they disappear from their owner's own
    /// list, totals and exports with no way to reach them through the API at all.
    ///
    /// A copy rather than a move: the category still belongs to the person who made it, and
    /// the household still needs it. An existing category of the user's with the same name and
    /// type is reused instead of duplicated — that is what the leaver would have created by
    /// hand, and a second copy would violate the unique index on (UserId, CategoryType, Name).
    /// </summary>
    private async Task ForkBorrowedCategoriesAsync(Guid userId, CancellationToken cancellationToken)
    {
        var transactions = await _context.Transactions
            .IgnoreQueryFilters()
            .Where(t => t.UserId == userId)
            .ToListAsync(cancellationToken);

        var templates = await _context.RecurringTransactions
            .IgnoreQueryFilters()
            .Where(r => r.UserId == userId)
            .ToListAsync(cancellationToken);

        var referenced = transactions.Select(t => t.CategoryId)
            .Concat(templates.Select(r => r.CategoryId))
            .Distinct()
            .ToList();

        if (referenced.Count == 0)
            return;

        var borrowed = await _context.Categories
            .IgnoreQueryFilters()
            .Where(c => c.UserId != userId && referenced.Contains(c.Id))
            .ToListAsync(cancellationToken);

        if (borrowed.Count == 0)
            return;

        var own = await _context.Categories
            .IgnoreQueryFilters()
            .Where(c => c.UserId == userId)
            .ToListAsync(cancellationToken);

        foreach (var source in borrowed)
        {
            // Case-insensitively, because SQL Server's default collation is, and the unique
            // index would reject a copy differing only in case.
            var replacement = own.FirstOrDefault(c =>
                c.CategoryType == source.CategoryType
                && string.Equals(c.Name, source.Name, StringComparison.OrdinalIgnoreCase));

            if (replacement is null)
            {
                replacement = new Category
                {
                    Id = Guid.NewGuid(),
                    Name = source.Name,
                    CategoryType = source.CategoryType,
                    UserId = userId,
                    HouseholdId = null,
                    CreatedAt = DateTime.UtcNow,
                    IsActive = source.IsActive
                };

                await _context.Categories.AddAsync(replacement, cancellationToken);
                own.Add(replacement);
            }

            foreach (var transaction in transactions.Where(t => t.CategoryId == source.Id))
                transaction.CategoryId = replacement.Id;

            foreach (var template in templates.Where(r => r.CategoryId == source.Id))
                template.CategoryId = replacement.Id;
        }
    }

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
