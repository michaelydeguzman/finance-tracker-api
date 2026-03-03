using FinanceTracker.Domain.Entities;

namespace FinanceTracker.Application.Dtos.Responses;

public sealed class CategoryResponseDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public CategoryType CategoryType { get; init; }
    public DateTime CreatedAt { get; init; }
    public bool IsActive { get; init; }

    public static CategoryResponseDto FromEntity(Category category)
        => new()
        {
            Id = category.Id,
            Name = category.Name,
            CategoryType = category.CategoryType,
            CreatedAt = category.CreatedAt,
            IsActive = category.IsActive
        };
}
