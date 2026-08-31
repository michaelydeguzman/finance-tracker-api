using FinanceTracker.Domain.Entities;

namespace FinanceTracker.Domain.Repositories;

public interface ICategoryRepository
{
    Task<Category> AddAsync(Category category);
    Task<Category?> GetByIdAsync(Guid id);
    Task<List<Category>> GetAllAsync();
    Task<List<Category>> GetByTypeAsync(CategoryType type);
    Task<bool> DeleteAsync(Guid id);
    Task<Category?> UpdateAsync(Category category);
}
