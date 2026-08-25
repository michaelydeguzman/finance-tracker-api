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
}
