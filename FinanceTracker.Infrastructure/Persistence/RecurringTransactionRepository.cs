using FinanceTracker.Domain.Entities;
using FinanceTracker.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Persistence;

public class RecurringTransactionRepository : IRecurringTransactionRepository
{
    private readonly FinanceTrackerContext _context;

    public RecurringTransactionRepository(FinanceTrackerContext context)
        => _context = context;

    public Task<List<RecurringTransaction>> GetActiveOverdueAsync(
        DateTime asOf,
        CancellationToken cancellationToken = default)
        => _context.RecurringTransactions
            // Deliberately cross-tenant: the worker materialises due templates for every
            // user, and each generated Transaction inherits its owner from its template.
            // This is the only read in the codebase that steps outside the tenancy filter.
            .IgnoreQueryFilters()
            .Include(r => r.Frequency)
            .Include(r => r.Category)
            .Where(r => r.Status == RecurringTransactionStatus.Active
                     && r.NextOccurrenceDate <= asOf)
            .ToListAsync(cancellationToken);

    public async Task<RecurringTransaction> AddAsync(
        RecurringTransaction template,
        CancellationToken cancellationToken = default)
    {
        _context.RecurringTransactions.Add(template);
        await _context.SaveChangesAsync(cancellationToken);
        return template;
    }

    // Every read below goes through the tenancy query filter — no IgnoreQueryFilters(), and
    // no hand-written UserId predicate that a later edit could drop.
    public Task<RecurringTransaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _context.RecurringTransactions
            .AsNoTracking()
            .Include(r => r.Category)
            .Include(r => r.Frequency)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public Task<RecurringTransaction?> GetTrackedByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _context.RecurringTransactions
            .Include(r => r.Category)
            .Include(r => r.Frequency)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public Task<List<RecurringTransaction>> GetAllAsync(
        RecurringTransactionStatus? status,
        CancellationToken cancellationToken = default)
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
            .ToListAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => _context.SaveChangesAsync(cancellationToken);

    public async Task DeleteAsync(RecurringTransaction template, CancellationToken cancellationToken = default)
    {
        _context.RecurringTransactions.Remove(template);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public Task<int> CountGeneratedTransactionsAsync(Guid templateId, CancellationToken cancellationToken = default)
        => _context.Transactions.CountAsync(t => t.RecurringTransactionId == templateId, cancellationToken);
}
