using FinanceTracker.Domain.Entities;

namespace FinanceTracker.Application.Dtos.Responses;

public sealed class TransactionResponseDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public Guid CategoryId { get; init; }
    public string CategoryName { get; init; } = string.Empty;
    public string CategoryType { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public DateTime TransactionDate { get; init; }

    /// <summary>
    /// The template that generated this row, or null for one the user entered by hand.
    /// </summary>
    public Guid? RecurringTransactionId { get; init; }

    /// <summary>
    /// The generating template's frequency. Null on a hand-entered transaction.
    ///
    /// Named to match what the front end already reads: <c>app/transactions/types/transaction.api.ts</c>
    /// declares <c>frequencyId</c> and <c>frequencyName</c> on <c>TransactionResponse</c>, and
    /// <c>transaction-entry-list.tsx</c> renders the "Recurrence" row from <c>frequencyName</c>.
    /// The API never sent either field, so that row was permanently blank.
    /// </summary>
    public Guid? FrequencyId { get; init; }

    /// <inheritdoc cref="FrequencyId"/>
    public string? FrequencyName { get; init; }

    public DateTime CreatedAt { get; init; }
    public string CreatedBy { get; init; } = string.Empty;

    public static TransactionResponseDto FromEntity(Transaction transaction)
        => new()
        {
            Id = transaction.Id,
            Name = transaction.Name,
            CategoryId = transaction.CategoryId,
            CategoryName = transaction.Category?.Name ?? string.Empty,
            CategoryType = transaction.Category?.CategoryType.ToString() ?? string.Empty,
            Description = transaction.Description,
            Amount = transaction.Amount,
            TransactionDate = transaction.TransactionDate,
            RecurringTransactionId = transaction.RecurringTransactionId,
            FrequencyId = transaction.RecurringTransaction?.FrequencyId,
            FrequencyName = transaction.RecurringTransaction?.Frequency?.Name,
            CreatedAt = transaction.CreatedAt,
            CreatedBy = transaction.CreatedBy
        };
}
