using FinanceTracker.Domain.Entities;

namespace FinanceTracker.Domain.Repositories;

/// <summary>How many records point at a category, counted across every tenant.</summary>
public sealed record CategoryUsage(int Transactions, int RecurringTransactions)
{
    public bool InUse => Transactions > 0 || RecurringTransactions > 0;
}

/// <summary>
/// The unique index on <c>(UserId, CategoryType, Name)</c> rejected a save. Callers check
/// <see cref="ICategoryRepository.NameTakenAsync"/> first, so this means two identical writes
/// raced past that check together.
/// </summary>
public sealed class CategoryNameTakenException : Exception
{
    public CategoryNameTakenException(Exception? innerException = null)
        : base("A category with that type and name already exists for its owner.", innerException) { }
}

/// <summary>
/// The <c>Restrict</c> foreign key rejected a delete. Callers check
/// <see cref="ICategoryRepository.GetUsageAsync"/> first, so this means a record was filed
/// under the category between that check and the delete.
/// </summary>
public sealed class CategoryInUseException : Exception
{
    public CategoryInUseException(Exception? innerException = null)
        : base("The category is still referenced by a transaction or recurring template.", innerException) { }
}

public interface ICategoryRepository
{
    /// <exception cref="CategoryNameTakenException">The unique index rejected the name.</exception>
    Task<Category> AddAsync(Category category, CancellationToken cancellationToken = default);
    Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<Category>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<List<Category>> GetByTypeAsync(CategoryType type, CancellationToken cancellationToken = default);
    /// <exception cref="CategoryInUseException">A foreign key still points at the category.</exception>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    /// <exception cref="CategoryNameTakenException">The unique index rejected the name.</exception>
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
