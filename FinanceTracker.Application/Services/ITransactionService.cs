using FinanceTracker.Application.Dtos;
using FinanceTracker.Domain.Entities;

namespace FinanceTracker.Application.Services;

public interface ITransactionService
{
    Task<Transaction> AddTransactionAsync(Transaction transaction, CancellationToken cancellationToken = default);
    Task<Transaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<Transaction>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<List<Transaction>> GetByCategoryType(CategoryType categoryType, CancellationToken cancellationToken = default);
    Task<Transaction?> UpdateTransactionAsync(Guid id, UpdateTransactionDto dto, CancellationToken cancellationToken = default);
    Task<bool> DeleteTransactionAsync(Guid id, CancellationToken cancellationToken = default);
}
