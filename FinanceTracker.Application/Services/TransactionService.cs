using FinanceTracker.Domain.Entities;
using FinanceTracker.Infrastructure.Persistence;

namespace FinanceTracker.Application.Services;

public class TransactionService : ITransactionService
{
    private readonly ITransactionRepository _repository;

    public TransactionService(ITransactionRepository repository)
    {
        _repository = repository;
    }

    public async Task<Transaction> AddTransactionAsync(Transaction transaction)
    {
        return await _repository.AddAsync(transaction);
    }

    public async Task<List<Transaction>> GetAllAsync()
    {
        return await _repository.GetAllAsync();
    }
}
