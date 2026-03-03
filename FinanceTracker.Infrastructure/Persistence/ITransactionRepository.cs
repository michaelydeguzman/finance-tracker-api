using FinanceTracker.Domain.Entities;

namespace FinanceTracker.Infrastructure.Persistence
{
    public interface ITransactionRepository
    {
        Task<Transaction> AddAsync(Transaction transaction);
        Task<Transaction?> GetByIdAsync(Guid id);
        Task<List<Transaction>> GetAllAsync();
    }
}
