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
                .Include(x => x.Frequency)
                .FirstOrDefaultAsync(x => x.Id == id);
        }

        public async Task<List<Transaction>> GetAllAsync()
        {
            return await _context.Transactions
                .AsNoTracking()
                .Include(x => x.Category)
                .Include(x => x.Frequency)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();
        }
    }
}
