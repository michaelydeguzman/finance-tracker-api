using FinanceTracker.Domain.Services;
using FinanceTracker.Application.Dtos;
using FinanceTracker.Domain.Entities;
using FinanceTracker.Infrastructure.Persistence;

namespace FinanceTracker.Application.Services;

public class TransactionService : ITransactionService
{
    private readonly ITransactionRepository _transactionRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly ICurrentUserAccessor _currentUser;

    public TransactionService(
        ITransactionRepository transactionRepository,
        ICategoryRepository categoryRepository,
        ICurrentUserAccessor currentUser)
    {
        _transactionRepository = transactionRepository;
        _categoryRepository = categoryRepository;
        _currentUser = currentUser;
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

    public async Task<Transaction?> UpdateTransactionAsync(Guid id, UpdateTransactionDto dto)
    {
        var transaction = new Transaction
        {
            Id = id,
            Name = dto.Name,
            CategoryId = dto.CategoryId,
            Category = null!,
            UserId = _currentUser.RequireUserId(),
            Description = dto.Description ?? string.Empty,
            Amount = dto.Amount,
            TransactionDate = dto.TransactionDate
        };

        return await _transactionRepository.UpdateAsync(transaction);
    }

    public async Task<bool> DeleteTransactionAsync(Guid id)
    {
        return await _transactionRepository.DeleteAsync(id);
    }
}
