using FinanceTracker.Domain.Entities;

namespace FinanceTracker.Application.Dtos.Responses;

public sealed class FrequencyResponseDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public FrequencyType Type { get; init; }
    public int? IntervalDays { get; init; }
    public string? Description { get; init; }
    public bool IsActive { get; init; }
    public DateTime CreatedAt { get; init; }

    public static FrequencyResponseDto FromEntity(Frequency frequency)
        => new()
        {
            Id = frequency.Id,
            Name = frequency.Name,
            Type = frequency.Type,
            IntervalDays = frequency.IntervalDays,
            Description = frequency.Description,
            IsActive = frequency.IsActive,
            CreatedAt = frequency.CreatedAt
        };
}
