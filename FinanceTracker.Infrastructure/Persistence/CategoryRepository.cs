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

        public async Task<Category> AddAsync(Category category, CancellationToken cancellationToken = default)
        {
            _context.Categories.Add(category);
            await _context.SaveChangesAsync(cancellationToken);
            return category;
        }

        public async Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await _context.Categories.FindAsync(new object?[] { id }, cancellationToken);
        }

        public async Task<List<Category>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return await _context.Categories.AsNoTracking().ToListAsync(cancellationToken);
        }

        public async Task<List<Category>> GetByTypeAsync(CategoryType type, CancellationToken cancellationToken = default)
        {
            return await _context.Categories.AsNoTracking()
                .Where(c => c.CategoryType == type)
                .ToListAsync(cancellationToken);
        }

        public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var category = await _context.Categories.FindAsync(new object?[] { id }, cancellationToken);
            if (category is null)
                return false;

            _context.Categories.Remove(category);
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }

        public async Task<Category?> UpdateAsync(Category category, CancellationToken cancellationToken = default)
        {
            var exists = await _context.Categories.AnyAsync(c => c.Id == category.Id, cancellationToken);
            if (!exists)
                return null;

            _context.Categories.Update(category);
            await _context.SaveChangesAsync(cancellationToken);
            return category;
        }
    }
}
