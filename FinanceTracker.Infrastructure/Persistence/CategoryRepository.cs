using FinanceTracker.Domain.Entities;
using FinanceTracker.Domain.Repositories;
using Microsoft.Data.SqlClient;
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
            await SaveNamedAsync(category, cancellationToken);
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
            await SaveNamedAsync(category, cancellationToken);
            return category;
        }

        /// <summary>
        /// Saves a category whose name the caller has already checked, translating the unique
        /// index rejecting it anyway — two identical writes racing past that check — into
        /// <see cref="CategoryNameTakenException"/>. The rejected entry is detached so it cannot
        /// poison a later save on the same context.
        /// </summary>
        private async Task SaveNamedAsync(Category category, CancellationToken cancellationToken)
        {
            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
            {
                _context.Entry(category).State = EntityState.Detached;
                throw new CategoryNameTakenException(ex);
            }
        }

        public async Task<CategoryUsage> GetUsageAsync(Guid categoryId, CancellationToken cancellationToken = default)
        {
            var transactions = await _context.Transactions.IgnoreQueryFilters()
                .CountAsync(t => t.CategoryId == categoryId, cancellationToken);
            var recurringTransactions = await _context.RecurringTransactions.IgnoreQueryFilters()
                .CountAsync(r => r.CategoryId == categoryId, cancellationToken);

            return new CategoryUsage(transactions, recurringTransactions);
        }

        public async Task<bool> NameTakenAsync(
            Guid ownerUserId,
            CategoryType categoryType,
            string name,
            Guid? exceptCategoryId = null,
            CancellationToken cancellationToken = default)
        {
            // Filters off: the index spans all of the owner's categories, including any the
            // caller cannot see â a housemate renaming someone else's category, say.
            return await _context.Categories.IgnoreQueryFilters()
                .AnyAsync(c => c.UserId == ownerUserId
                               && c.CategoryType == categoryType
                               && c.Name == name
                               && c.Id != exceptCategoryId,
                    cancellationToken);
        }
    }
}
