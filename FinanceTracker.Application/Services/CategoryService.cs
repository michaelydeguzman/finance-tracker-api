using FinanceTracker.Domain.Entities;
using FinanceTracker.Infrastructure.Persistence;

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
            // Business rules can be added here
            return await _repository.AddAsync(category);
        }

        public async Task<Category?> GetCategoryByIdAsync(Guid id)
        {
            return await _repository.GetByIdAsync(id);
        }
    }
}
