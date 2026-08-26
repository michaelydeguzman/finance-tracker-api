using System.Text.Json.Serialization;
using FinanceTracker.Domain.Entities;

namespace FinanceTracker.Application.Dtos.Responses;

/// <summary>
/// A recurring template as the front end reads it.
///
/// Enum wire formats are not chosen field by field on taste. Each one matches the format the
/// front end already receives for that same concept elsewhere, so nothing here needs a new
/// parsing rule:
///
/// <list type="bullet">
/// <item><c>categoryType</c> is a string, as <see cref="TransactionResponseDto"/> already writes it.</item>
/// <item><c>frequencyType</c> is a number, as <see cref="FrequencyResponseDto"/> already writes it
/// and <c>types/shared/enums.ts</c> already declares it.</item>
/// <item><c>status</c> is a string, via a converter scoped to that one property. It is a new
/// contract with no numeric consumer, the column itself is persisted as a string, and a badge
/// label is what the caller actually wants. The converter is deliberately not global: registering
/// it globally would also rewrite <c>categoryType</c> as "Expense" and break every category screen.</item>
/// </list>
/// </summary>
public sealed class RecurringTransactionResponseDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }

    /// <summary>The template's <c>DefaultAmount</c> — what each generated transaction is worth.</summary>
    public decimal Amount { get; init; }

    public Guid CategoryId { get; init; }
    public string CategoryName { get; init; } = string.Empty;
    public string CategoryType { get; init; } = string.Empty;

    public Guid FrequencyId { get; init; }
    public string FrequencyName { get; init; } = string.Empty;
    public FrequencyType FrequencyType { get; init; }

    public DateTime StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public DateTime NextOccurrenceDate { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public RecurringTransactionStatus Status { get; init; }

    public DateTime CreatedAt { get; init; }
    public string CreatedBy { get; init; } = string.Empty;

    public static RecurringTransactionResponseDto FromEntity(RecurringTransaction template)
        => new()
        {
            Id = template.Id,
            Name = template.Name,
            Description = template.Description,
            Amount = template.DefaultAmount,
            CategoryId = template.CategoryId,
            CategoryName = template.Category?.Name ?? string.Empty,
            CategoryType = template.Category?.CategoryType.ToString() ?? string.Empty,
            FrequencyId = template.FrequencyId,
            FrequencyName = template.Frequency?.Name ?? string.Empty,
            FrequencyType = template.Frequency?.Type ?? default,
            StartDate = template.StartDate,
            EndDate = template.EndDate,
            NextOccurrenceDate = template.NextOccurrenceDate,
            Status = template.Status,
            CreatedAt = template.CreatedAt,
            CreatedBy = template.CreatedBy
        };
}
