using FinanceTracker.Domain.Entities;

namespace FinanceTracker.Domain.Repositories;

public interface ITransactionRepository
{
    Task<Transaction> AddAsync(Transaction transaction, CancellationToken cancellationToken = default);
    Task<Transaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<Transaction>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Composable query for callers that add their own filtering and paging. No token here:
    /// nothing is executed until the caller enumerates it, and that call site passes its own.
    /// </summary>
    IQueryable<Transaction> GetTransactionsQueryable();

    Task<Transaction?> UpdateAsync(Transaction transaction, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
