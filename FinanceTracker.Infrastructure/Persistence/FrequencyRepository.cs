using FinanceTracker.Domain.Entities;
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

        public async Task<List<Frequency>> GetAllAsync()
        {
            return await _context.Frequencies
                .AsNoTracking()
                .OrderBy(x => x.Name)
                .ToListAsync();
        }

        public async Task<Frequency?> GetByIdAsync(Guid id)
        {
            return await _context.Frequencies
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);
        }
    }
}
