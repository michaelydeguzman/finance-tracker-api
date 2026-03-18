using FinanceTracker.Domain.Entities;

namespace FinanceTracker.Application.Services;

public interface ITransactionService
{
    Task<Transaction> AddTransactionAsync(Transaction transaction);
    Task<Transaction?> GetByIdAsync(Guid id);
    Task<List<Transaction>> GetAllAsync();
    Task<List<Transaction>> GetByCategoryType(CategoryType categoryType);
}
