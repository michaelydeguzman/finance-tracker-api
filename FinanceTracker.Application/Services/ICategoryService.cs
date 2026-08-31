using FinanceTracker.Domain.Entities;

namespace FinanceTracker.Application.Services
{
    public interface ICategoryService
    {
        Task<Category> AddCategoryAsync(Category category, CancellationToken cancellationToken = default);
        Task<Category?> GetCategoryByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task<List<Category>> GetAllAsync(CancellationToken cancellationToken = default);
        Task<List<Category>> GetByTypeAsync(CategoryType type, CancellationToken cancellationToken = default);
        Task<bool> DeleteCategoryAsync(Guid id, CancellationToken cancellationToken = default);
        Task<Category?> UpdateCategoryAsync(Guid id, string name, CategoryType categoryType, CancellationToken cancellationToken = default);
    }
}
