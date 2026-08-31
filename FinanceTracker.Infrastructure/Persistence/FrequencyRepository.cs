using FinanceTracker.Domain.Entities;
using FinanceTracker.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Persistence
{
    public class FrequencyRepository : IFrequencyRepository
    {
        private readonly FinanceTrackerContext _context;

        public FrequencyRepository(FinanceTrackerContext context)
        {
            _context = context;
        }

        public async Task<List<Frequency>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return await _context.Frequencies
                .AsNoTracking()
                .OrderBy(x => x.Name)
                .ToListAsync(cancellationToken);
        }

        public async Task<Frequency?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await _context.Frequencies
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        }
    }
}
