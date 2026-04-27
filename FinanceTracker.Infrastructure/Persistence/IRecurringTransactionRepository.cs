using FinanceTracker.Domain.Entities;

namespace FinanceTracker.Infrastructure.Persistence;

public interface IRecurringTransactionRepository
{
    Task<List<RecurringTransaction>> GetActiveOverdueAsync(DateTime asOf);
}
