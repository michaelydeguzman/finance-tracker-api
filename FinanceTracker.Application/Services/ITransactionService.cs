using FinanceTracker.Application.Dtos;
using FinanceTracker.Domain.Entities;

namespace FinanceTracker.Application.Services;

public interface ITransactionService
{
    Task<Transaction> AddTransactionAsync(Transaction transaction);
    Task<Transaction?> GetByIdAsync(Guid id);
    Task<List<Transaction>> GetAllAsync();
    Task<List<Transaction>> GetByCategoryType(CategoryType categoryType);
    Task<Transaction?> UpdateTransactionAsync(Guid id, UpdateTransactionDto dto);
    Task<bool> DeleteTransactionAsync(Guid id);
}
