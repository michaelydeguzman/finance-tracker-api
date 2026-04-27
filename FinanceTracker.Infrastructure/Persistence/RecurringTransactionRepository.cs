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
            .Include(r => r.Frequency)
            .Include(r => r.Category)
            .Where(r => r.Status == RecurringTransactionStatus.Active
                     && r.NextOccurrenceDate <= asOf)
            .ToListAsync();
}
