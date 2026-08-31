using FinanceTracker.Domain.Entities;

namespace FinanceTracker.Domain.Repositories;

public interface ICategoryRepository
{
    Task<Category> AddAsync(Category category, CancellationToken cancellationToken = default);
    Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<Category>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<List<Category>> GetByTypeAsync(CategoryType type, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Category?> UpdateAsync(Category category, CancellationToken cancellationToken = default);
}
