using FinanceTracker.Domain.Entities;

namespace FinanceTracker.Application.Services
{
    public interface IFrequencyService
    {
        Task<List<Frequency>> GetAllAsync();
    }
}
