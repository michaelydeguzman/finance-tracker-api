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
            CreatedAt = transaction.CreatedAt,
            CreatedBy = transaction.CreatedBy
        };
}
