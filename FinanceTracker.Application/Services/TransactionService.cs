using FinanceTracker.Domain.Services;
using FinanceTracker.Application.Dtos;
using FinanceTracker.Domain.Entities;
using FinanceTracker.Domain.Repositories;

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

    public async Task<Transaction> AddTransactionAsync(Transaction transaction, CancellationToken cancellationToken = default)
    {
        return await _transactionRepository.AddAsync(transaction, cancellationToken);
    }

    public async Task<Transaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _transactionRepository.GetByIdAsync(id, cancellationToken);
    }

    public async Task<List<Transaction>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _transactionRepository.GetAllAsync(cancellationToken);
    }

    public async Task<List<Transaction>> GetByCategoryType(CategoryType categoryType, CancellationToken cancellationToken = default)
    {
        var categories = await _categoryRepository.GetByTypeAsync(categoryType, cancellationToken);
        var categoryIds = categories.Select(c => c.Id).ToHashSet();

        var transactions = await _transactionRepository.GetAllAsync(cancellationToken);
        return transactions.Where(t => categoryIds.Contains(t.CategoryId)).ToList();
    }

    public async Task<Transaction?> UpdateTransactionAsync(Guid id, UpdateTransactionDto dto, CancellationToken cancellationToken = default)
    {
        // The category lookup is tenancy-scoped, so this doubles as the reachability check —
        // the same thing the recurring handlers already do. Without it any GUID satisfying
        // the foreign key is accepted, including a category from a household the caller has
        // left: the row then vanishes from every member's list, because a Transaction's
        // Category is a required navigation and the filter hides it.
        var category = await _categoryRepository.GetByIdAsync(dto.CategoryId, cancellationToken);

        if (category is null)
            return null;

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

        return await _transactionRepository.UpdateAsync(transaction, cancellationToken);
    }

    public async Task<bool> DeleteTransactionAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _transactionRepository.DeleteAsync(id, cancellationToken);
    }
}
