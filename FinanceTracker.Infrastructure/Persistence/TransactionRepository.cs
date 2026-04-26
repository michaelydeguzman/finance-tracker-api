using FinanceTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Persistence
{
    public class TransactionRepository : ITransactionRepository
    {
        private readonly FinanceTrackerContext _context;

        public TransactionRepository(FinanceTrackerContext context)
        {
            _context = context;
        }

        public async Task<Transaction> AddAsync(Transaction transaction)
        {
            _context.Transactions.Add(transaction);
            await _context.SaveChangesAsync();
            return transaction;
        }

        public async Task<Transaction?> GetByIdAsync(Guid id)
        {
            return await _context.Transactions
                .AsNoTracking()
                .Include(x => x.Category)
                .FirstOrDefaultAsync(x => x.Id == id);
        }

        public IQueryable<Transaction> GetTransactionsQueryable()
        {
            return _context.Transactions
                .AsNoTracking()
                .Include(x => x.Category);
        }

        public async Task<List<Transaction>> GetAllAsync()
        {
            return await GetTransactionsQueryable()
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();
        }

        public async Task<Transaction?> UpdateAsync(Transaction transaction)
        {
            var entity = await _context.Transactions.FindAsync(transaction.Id);
            if (entity is null)
                return null;

            entity.Name = transaction.Name;
            entity.CategoryId = transaction.CategoryId;
            entity.Description = transaction.Description;
            entity.Amount = transaction.Amount;
            entity.TransactionDate = transaction.TransactionDate;

            await _context.SaveChangesAsync();
            return entity;
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            var entity = await _context.Transactions.FindAsync(id);
            if (entity is null)
                return false;

            _context.Transactions.Remove(entity);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
