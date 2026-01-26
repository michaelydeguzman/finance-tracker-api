using FinanceTracker.Domain.Entities;

namespace FinanceTracker.Infrastructure.Persistence
{
    public interface ICategoryRepository
    {
        Task<Category> AddAsync(Category category);
        Task<Category?> GetByIdAsync(Guid id);
        Task<List<Category>> GetAllAsync();
        Task<List<Category>> GetByTypeAsync(CategoryTypes type);
    }
}
