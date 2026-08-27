using FinanceTracker.Domain.Entities;

namespace FinanceTracker.Infrastructure.Persistence;

public interface IRecurringTransactionRepository
{
    /// <summary>
    /// The worker's cross-tenant sweep. Every other member here is tenancy-scoped by the
    /// model-level query filter and must stay that way.
    /// </summary>
    Task<List<RecurringTransaction>> GetActiveOverdueAsync(DateTime asOf);

    Task<RecurringTransaction> AddAsync(RecurringTransaction template);

    /// <summary>Read-only projection for responses. Returns null for another tenant's id.</summary>
    Task<RecurringTransaction?> GetByIdAsync(Guid id);

    /// <summary>
    /// Tracked, for the paths that mutate. Returns null for another tenant's id, which is
    /// what turns a cross-tenant update or transition into a 404.
    /// </summary>
    Task<RecurringTransaction?> GetTrackedByIdAsync(Guid id);

    Task<List<RecurringTransaction>> GetAllAsync(RecurringTransactionStatus? status);

    Task SaveChangesAsync();

    Task DeleteAsync(RecurringTransaction template);

    /// <summary>
    /// How many transactions this template has already generated. Deleting a template that
    /// has any would strand them: the FK is <c>SetNull</c>, so the rows survive with no
    /// record of where they came from.
    /// </summary>
    Task<int> CountGeneratedTransactionsAsync(Guid templateId);
}
