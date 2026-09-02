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

    public async Task StampRecordsAsync(
        Guid userId,
        Guid householdId,
        CancellationToken cancellationToken = default)
    {
        // IgnoreQueryFilters throughout. The caller is mid-transition — not yet in the
        // household whose id is being written — so the filter would hide the very rows this
        // exists to move.
        var categories = await _context.Categories
            .IgnoreQueryFilters()
            .Where(c => c.UserId == userId)
            .ToListAsync(cancellationToken);

        foreach (var category in categories)
            category.HouseholdId = householdId;

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
    }

    public async Task DetachRecordsAsync(
        Guid userId,
        Guid householdId,
        CancellationToken cancellationToken = default)
    {
        var transactions = await _context.Transactions
            .IgnoreQueryFilters()
            .Where(t => t.UserId == userId)
            .ToListAsync(cancellationToken);

        foreach (var transaction in transactions)
            transaction.HouseholdId = null;

        var templates = await _context.RecurringTransactions
            .IgnoreQueryFilters()
            .Where(r => r.UserId == userId)
            .ToListAsync(cancellationToken);

        foreach (var template in templates)
            template.HouseholdId = null;

        // Read before the categories, and from the database rather than the change tracker,
        // so it reflects what other members still hold. The rows nulled above are excluded
        // by owner anyway.
        var stillInUse = await CategoryIdsUsedByOthersAsync(householdId, userId, cancellationToken);

        var categories = await _context.Categories
            .IgnoreQueryFilters()
            .Where(c => c.UserId == userId)
            .ToListAsync(cancellationToken);

        foreach (var category in categories)
        {
            // A category another member's record points at stays with the household. See
            // IHouseholdRepository.DetachRecordsAsync for why leaving would hide that
            // member's own transaction from them.
            if (!stillInUse.Contains(category.Id))
                category.HouseholdId = null;
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
    /// Categories that records belonging to somebody other than
    /// <paramref name="excludingUserId"/> still point at, within this household.
    ///
    /// Two queries rather than one <c>Union</c>: the InMemory provider the tests run on is
    /// the weakest link in the translation chain, and this costs nothing at these volumes.
    /// </summary>
    private async Task<HashSet<Guid>> CategoryIdsUsedByOthersAsync(
        Guid householdId,
        Guid excludingUserId,
        CancellationToken cancellationToken)
    {
        var fromTransactions = await _context.Transactions
            .IgnoreQueryFilters()
            .Where(t => t.HouseholdId == householdId && t.UserId != excludingUserId)
            .Select(t => t.CategoryId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var fromTemplates = await _context.RecurringTransactions
            .IgnoreQueryFilters()
            .Where(r => r.HouseholdId == householdId && r.UserId != excludingUserId)
            .Select(r => r.CategoryId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return fromTransactions.Concat(fromTemplates).ToHashSet();
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _context.SaveChangesAsync(cancellationToken);
}
