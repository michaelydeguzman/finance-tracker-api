using FinanceTracker.Application.Dtos.Responses;
using FinanceTracker.Application.Services;
using MediatR;

namespace FinanceTracker.Application.Features.Categories.Queries.GetCategoryById;

public sealed class GetCategoryByIdQueryHandler : IRequestHandler<GetCategoryByIdQuery, CategoryResponseDto?>
{
    private readonly ICategoryService _categoryService;

    public GetCategoryByIdQueryHandler(ICategoryService categoryService)
    {
        _categoryService = categoryService;
    }

    public async Task<CategoryResponseDto?> Handle(GetCategoryByIdQuery request, CancellationToken cancellationToken)
    {
        var category = await _categoryService.GetCategoryByIdAsync(request.Id);
        return category is null ? null : CategoryResponseDto.FromEntity(category);
    }
}
