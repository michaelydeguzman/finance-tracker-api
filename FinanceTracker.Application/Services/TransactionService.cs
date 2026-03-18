using FinanceTracker.Domain.Entities;
using FinanceTracker.Infrastructure.Persistence;

namespace FinanceTracker.Application.Services;

public class TransactionService : ITransactionService
{
    private readonly ITransactionRepository _transactionRepository;
    private readonly ICategoryRepository _categoryRepository;

    public TransactionService(ITransactionRepository transactionRepository, ICategoryRepository categoryRepository)
    {
        _transactionRepository = transactionRepository;
        _categoryRepository = categoryRepository;
    }

    public async Task<Transaction> AddTransactionAsync(Transaction transaction)
    {
        return await _transactionRepository.AddAsync(transaction);
    }

    public async Task<Transaction?> GetByIdAsync(Guid id)
    {
        return await _transactionRepository.GetByIdAsync(id);
    }

    public async Task<List<Transaction>> GetAllAsync()
    {
        return await _transactionRepository.GetAllAsync();
    }

    public async Task<List<Transaction>> GetByCategoryType(CategoryType categoryType)
    {
        var categories = await _categoryRepository.GetByTypeAsync(categoryType);
        var categoryIds = categories.Select(c => c.Id).ToHashSet();

        var transactions = await _transactionRepository.GetAllAsync();
        return transactions.Where(t => categoryIds.Contains(t.CategoryId)).ToList();
    }
}
