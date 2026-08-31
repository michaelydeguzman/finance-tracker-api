using FinanceTracker.Domain.Entities;
using FinanceTracker.Domain.Repositories;

namespace FinanceTracker.Application.Services
{
    public class FrequencyService : IFrequencyService
    {
        private readonly IFrequencyRepository _repository;

        public FrequencyService(IFrequencyRepository repository)
        {
            _repository = repository;
        }

        public async Task<List<Frequency>> GetAllAsync()
        {
            return await _repository.GetAllAsync();
        }
    }
}
