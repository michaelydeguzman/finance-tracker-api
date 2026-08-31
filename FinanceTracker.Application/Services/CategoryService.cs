using FinanceTracker.Domain.Entities;
using FinanceTracker.Domain.Repositories;

namespace FinanceTracker.Application.Services
{
    public class CategoryService : ICategoryService
    {
        private readonly ICategoryRepository _repository;

        public CategoryService(ICategoryRepository repository)
        {
            _repository = repository;
        }

        public async Task<Category> AddCategoryAsync(Category category)
        {
            return await _repository.AddAsync(category);
        }

        public async Task<Category?> GetCategoryByIdAsync(Guid id)
        {
            return await _repository.GetByIdAsync(id);
        }

        public async Task<List<Category>> GetAllAsync()
        {
            return await _repository.GetAllAsync();
        }

        public async Task<List<Category>> GetByTypeAsync(CategoryType type)
        {
            return await _repository.GetByTypeAsync(type);
        }

        public async Task<bool> DeleteCategoryAsync(Guid id)
        {
            return await _repository.DeleteAsync(id);
        }

        public async Task<Category?> UpdateCategoryAsync(Guid id, string name, CategoryType categoryType)
        {
            var category = await _repository.GetByIdAsync(id);
            if (category is null)
                return null;

            category.Name = name;
            category.CategoryType = categoryType;

            return await _repository.UpdateAsync(category);
        }
    }
}
