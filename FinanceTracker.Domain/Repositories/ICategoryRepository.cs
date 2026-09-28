using FinanceTracker.Domain.Entities;

namespace FinanceTracker.Domain.Repositories;

/// <summary>How many records point at a category, counted across every tenant.</summary>
public sealed record CategoryUsage(int Transactions, int RecurringTransactions)
{
    public bool InUse => Transactions > 0 || RecurringTransactions > 0;
}

public interface ICategoryRepository
{
    Task<Category> AddAsync(Category category, CancellationToken cancellationToken = default);
    Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<Category>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<List<Category>> GetByTypeAsync(CategoryType type, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Category?> UpdateAsync(Category category, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every transaction and recurring template filed under the category, whoever owns them —
    /// not just the ones the caller can see. The foreign keys that make deleting a used
    /// category unsafe are enforced by the database, which knows nothing of the query filters.
    /// </summary>
    Task<CategoryUsage> GetUsageAsync(Guid categoryId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether <paramref name="ownerUserId"/> already has a category of this type and name,
    /// other than <paramref name="exceptCategoryId"/>. Mirrors the unique index on
    /// <c>(UserId, CategoryType, Name)</c>, and is answered by the database so its comparison
    /// is the one the index enforces.
    /// </summary>
    Task<bool> NameTakenAsync(
        Guid ownerUserId,
        CategoryType categoryType,
        string name,
        Guid? exceptCategoryId = null,
        CancellationToken cancellationToken = default);
}
