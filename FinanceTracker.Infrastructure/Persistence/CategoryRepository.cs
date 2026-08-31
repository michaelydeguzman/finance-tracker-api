using FinanceTracker.Domain.Entities;
using FinanceTracker.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Persistence
{
    public class CategoryRepository : ICategoryRepository
    {
        private readonly FinanceTrackerContext _context;

        public CategoryRepository(FinanceTrackerContext context)
        {
            _context = context;
        }

        public async Task<Category> AddAsync(Category category)
        {
            _context.Categories.Add(category);
            await _context.SaveChangesAsync();
            return category;
        }

        public async Task<Category?> GetByIdAsync(Guid id)
        {
            return await _context.Categories.FindAsync(id);
        }

        public async Task<List<Category>> GetAllAsync()
        {
            return await _context.Categories.AsNoTracking().ToListAsync();
        }

        public async Task<List<Category>> GetByTypeAsync(CategoryType type)
        {
            return await _context.Categories.AsNoTracking()
                .Where(c => c.CategoryType == type)
                .ToListAsync();
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            var category = await _context.Categories.FindAsync(id);
            if (category is null)
                return false;

            _context.Categories.Remove(category);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<Category?> UpdateAsync(Category category)
        {
            var exists = await _context.Categories.AnyAsync(c => c.Id == category.Id);
            if (!exists)
                return null;

            _context.Categories.Update(category);
            await _context.SaveChangesAsync();
            return category;
        }
    }
}
