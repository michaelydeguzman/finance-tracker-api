using FinanceTracker.Application.Dtos.Responses;
using FinanceTracker.Application.Services;
using FinanceTracker.Domain.Entities;
using MediatR;

namespace FinanceTracker.Application.Features.Categories.Commands.CreateCategory;

public sealed class CreateCategoryCommandHandler : IRequestHandler<CreateCategoryCommand, CategoryResponseDto>
{
    private readonly ICategoryService _categoryService;

    public CreateCategoryCommandHandler(ICategoryService categoryService)
    {
        _categoryService = categoryService;
    }

    public async Task<CategoryResponseDto> Handle(CreateCategoryCommand request, CancellationToken cancellationToken)
    {
        var category = new Category
        {
            Id = Guid.NewGuid(),
            Name = request.Dto.Name,
            CategoryType = request.Dto.CategoryType,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        var created = await _categoryService.AddCategoryAsync(category);
        return CategoryResponseDto.FromEntity(created);
    }
}
