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

    public async Task ReassignRecordsAsync(
        Guid userId,
        Guid? householdId,
        CancellationToken cancellationToken = default)
    {
        // IgnoreQueryFilters throughout. The caller is mid-transition — either not yet in the
        // household whose id is being written, or no longer in the one being cleared — so the
        // filter would hide the very rows this exists to move.
        //
        // Owner alone, with no "and the household differs" narrowing: comparing a column
        // against a null parameter is where null semantics get subtle, and EF marks an
        // unchanged assignment as clean anyway, so the narrowing would buy nothing.
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

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _context.SaveChangesAsync(cancellationToken);
}
