using FinanceTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Persistence;

public class RecurringTransactionRepository : IRecurringTransactionRepository
{
    private readonly FinanceTrackerContext _context;

    public RecurringTransactionRepository(FinanceTrackerContext context)
        => _context = context;

    public Task<List<RecurringTransaction>> GetActiveOverdueAsync(DateTime asOf)
        => _context.RecurringTransactions
            // Deliberately cross-tenant: the worker materialises due templates for every
            // user, and each generated Transaction inherits its owner from its template.
            // This is the only read in the codebase that steps outside the tenancy filter.
            .IgnoreQueryFilters()
            .Include(r => r.Frequency)
            .Include(r => r.Category)
            .Where(r => r.Status == RecurringTransactionStatus.Active
                     && r.NextOccurrenceDate <= asOf)
            .ToListAsync();

    public async Task<RecurringTransaction> AddAsync(RecurringTransaction template)
    {
        _context.RecurringTransactions.Add(template);
        await _context.SaveChangesAsync();
        return template;
    }

    // Every read below goes through the tenancy query filter — no IgnoreQueryFilters(), and
    // no hand-written UserId predicate that a later edit could drop.
    public Task<RecurringTransaction?> GetByIdAsync(Guid id)
        => _context.RecurringTransactions
            .AsNoTracking()
            .Include(r => r.Category)
            .Include(r => r.Frequency)
            .FirstOrDefaultAsync(r => r.Id == id);

    public Task<RecurringTransaction?> GetTrackedByIdAsync(Guid id)
        => _context.RecurringTransactions
            .Include(r => r.Category)
            .Include(r => r.Frequency)
            .FirstOrDefaultAsync(r => r.Id == id);

    public Task<List<RecurringTransaction>> GetAllAsync(RecurringTransactionStatus? status)
    {
        var query = _context.RecurringTransactions
            .AsNoTracking()
            .Include(r => r.Category)
            .Include(r => r.Frequency)
            .AsQueryable();

        if (status is { } wanted)
            query = query.Where(r => r.Status == wanted);

        // Soonest-due first: the list's most useful ordering is "what is about to happen".
        return query
            .OrderBy(r => r.NextOccurrenceDate)
            .ThenBy(r => r.Name)
            .ToListAsync();
    }

    public Task SaveChangesAsync() => _context.SaveChangesAsync();

    public async Task DeleteAsync(RecurringTransaction template)
    {
        _context.RecurringTransactions.Remove(template);
        await _context.SaveChangesAsync();
    }

    public Task<int> CountGeneratedTransactionsAsync(Guid templateId)
        => _context.Transactions.CountAsync(t => t.RecurringTransactionId == templateId);
}
