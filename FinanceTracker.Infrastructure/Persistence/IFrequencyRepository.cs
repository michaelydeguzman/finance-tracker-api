using FinanceTracker.Domain.Entities;

namespace FinanceTracker.Infrastructure.Persistence
{
    public interface IFrequencyRepository
    {
        Task<List<Frequency>> GetAllAsync();
    }
}
