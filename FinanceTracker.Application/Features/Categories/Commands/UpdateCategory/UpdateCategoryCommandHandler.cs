using FinanceTracker.Application.Dtos.Responses;
using FinanceTracker.Application.Services;
using MediatR;

namespace FinanceTracker.Application.Features.Categories.Commands.UpdateCategory;

public sealed class UpdateCategoryCommandHandler : IRequestHandler<UpdateCategoryCommand, CategoryResponseDto?>
{
    private readonly ICategoryService _categoryService;

    public UpdateCategoryCommandHandler(ICategoryService categoryService)
    {
        _categoryService = categoryService;
    }

    public async Task<CategoryResponseDto?> Handle(UpdateCategoryCommand request, CancellationToken cancellationToken)
    {
        var updated = await _categoryService.UpdateCategoryAsync(request.Id, request.Dto.Name, request.Dto.CategoryType, cancellationToken);
        return updated is null ? null : CategoryResponseDto.FromEntity(updated);
    }
}
