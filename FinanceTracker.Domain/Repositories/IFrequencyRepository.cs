using FinanceTracker.Domain.Entities;

namespace FinanceTracker.Domain.Repositories;

public interface IFrequencyRepository
{
    Task<List<Frequency>> GetAllAsync();

    /// <summary>
    /// Frequencies are shared reference data, deliberately outside the tenancy filter,
    /// so this is not a tenant-scoped lookup and must never be used as one.
    /// </summary>
    Task<Frequency?> GetByIdAsync(Guid id);
}
