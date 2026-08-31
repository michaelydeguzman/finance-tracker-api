using FinanceTracker.Domain.Entities;

namespace FinanceTracker.Domain.Repositories;

public interface IRecurringTransactionRepository
{
    /// <summary>
    /// The worker's cross-tenant sweep. Every other member here is tenancy-scoped by the
    /// model-level query filter and must stay that way.
    /// </summary>
    Task<List<RecurringTransaction>> GetActiveOverdueAsync(DateTime asOf, CancellationToken cancellationToken = default);

    Task<RecurringTransaction> AddAsync(RecurringTransaction template, CancellationToken cancellationToken = default);

    /// <summary>Read-only projection for responses. Returns null for another tenant's id.</summary>
    Task<RecurringTransaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tracked, for the paths that mutate. Returns null for another tenant's id, which is
    /// what turns a cross-tenant update or transition into a 404.
    /// </summary>
    Task<RecurringTransaction?> GetTrackedByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<List<RecurringTransaction>> GetAllAsync(RecurringTransactionStatus? status, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    Task DeleteAsync(RecurringTransaction template, CancellationToken cancellationToken = default);

    /// <summary>
    /// How many transactions this template has already generated. Deleting a template that
    /// has any would strand them: the FK is <c>SetNull</c>, so the rows survive with no
    /// record of where they came from.
    /// </summary>
    Task<int> CountGeneratedTransactionsAsync(Guid templateId, CancellationToken cancellationToken = default);
}
