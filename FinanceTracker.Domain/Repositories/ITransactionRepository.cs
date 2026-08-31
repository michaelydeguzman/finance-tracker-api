using FinanceTracker.Domain.Entities;

namespace FinanceTracker.Domain.Repositories;

public interface ITransactionRepository
{
    Task<Transaction> AddAsync(Transaction transaction);
    Task<Transaction?> GetByIdAsync(Guid id);
    Task<List<Transaction>> GetAllAsync();
    IQueryable<Transaction> GetTransactionsQueryable();
    Task<Transaction?> UpdateAsync(Transaction transaction);
    Task<bool> DeleteAsync(Guid id);
}
