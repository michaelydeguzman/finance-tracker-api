using FinanceTracker.Domain.Entities;

namespace FinanceTracker.Application.Services;

public interface ITransactionService
{
    Task<Transaction> AddTransactionAsync(Transaction transaction);
    Task<List<Transaction>> GetAllAsync();
}
