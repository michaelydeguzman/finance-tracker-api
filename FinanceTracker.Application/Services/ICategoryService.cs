using FinanceTracker.Domain.Entities;

namespace FinanceTracker.Application.Services
{
    public interface ICategoryService
    {
        Task<Category> AddCategoryAsync(Category category);
        Task<Category?> GetCategoryByIdAsync(Guid id);
        Task<List<Category>> GetAllAsync();
        Task<List<Category>> GetByTypeAsync(CategoryType type);
        Task<bool> DeleteCategoryAsync(Guid id);
        Task<Category?> UpdateCategoryAsync(Guid id, string name, CategoryType categoryType);
    }
}
