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

        public async Task<Category> AddCategoryAsync(Category category, CancellationToken cancellationToken = default)
        {
            return await _repository.AddAsync(category, cancellationToken);
        }

        public async Task<Category?> GetCategoryByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await _repository.GetByIdAsync(id, cancellationToken);
        }

        public async Task<List<Category>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return await _repository.GetAllAsync(cancellationToken);
        }

        public async Task<List<Category>> GetByTypeAsync(CategoryType type, CancellationToken cancellationToken = default)
        {
            return await _repository.GetByTypeAsync(type, cancellationToken);
        }

        public async Task<bool> DeleteCategoryAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await _repository.DeleteAsync(id, cancellationToken);
        }

        public async Task<Category?> UpdateCategoryAsync(
            Guid id,
            string name,
            CategoryType categoryType,
            CancellationToken cancellationToken = default)
        {
            var category = await _repository.GetByIdAsync(id, cancellationToken);
            if (category is null)
                return null;

            category.Name = name;
            category.CategoryType = categoryType;

            return await _repository.UpdateAsync(category, cancellationToken);
        }
    }
}
