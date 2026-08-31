using FinanceTracker.Domain.Entities;
using FinanceTracker.Domain.Repositories;
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

        public async Task<Transaction> AddAsync(Transaction transaction, CancellationToken cancellationToken = default)
        {
            _context.Transactions.Add(transaction);
            await _context.SaveChangesAsync(cancellationToken);
            return transaction;
        }

        public async Task<Transaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await _context.Transactions
                .AsNoTracking()
                .Include(x => x.Category)
                // The generating template carries the frequency the UI labels the row with.
                // Without this the "Recurrence" detail row is blank on every generated
                // transaction, because the navigation is simply never loaded.
                .Include(x => x.RecurringTransaction!)
                    .ThenInclude(r => r.Frequency)
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        }

        public IQueryable<Transaction> GetTransactionsQueryable()
        {
            return _context.Transactions
                .AsNoTracking()
                .Include(x => x.Category)
                .Include(x => x.RecurringTransaction!)
                    .ThenInclude(r => r.Frequency);
        }

        public async Task<List<Transaction>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return await GetTransactionsQueryable()
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync(cancellationToken);
        }

        public async Task<Transaction?> UpdateAsync(Transaction transaction, CancellationToken cancellationToken = default)
        {
            var entity = await _context.Transactions.FindAsync(new object?[] { transaction.Id }, cancellationToken);
            if (entity is null)
                return null;

            entity.Name = transaction.Name;
            entity.CategoryId = transaction.CategoryId;
            entity.Description = transaction.Description;
            entity.Amount = transaction.Amount;
            entity.TransactionDate = transaction.TransactionDate;

            await _context.SaveChangesAsync(cancellationToken);
            return entity;
        }

        public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var entity = await _context.Transactions.FindAsync(new object?[] { id }, cancellationToken);
            if (entity is null)
                return false;

            _context.Transactions.Remove(entity);
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }
    }
}
