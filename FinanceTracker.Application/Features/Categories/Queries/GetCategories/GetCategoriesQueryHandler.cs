using FinanceTracker.Application.Dtos.Responses;
using FinanceTracker.Application.Services;
using MediatR;

namespace FinanceTracker.Application.Features.Categories.Queries.GetCategories;

public sealed class GetCategoriesQueryHandler : IRequestHandler<GetCategoriesQuery, List<CategoryResponseDto>>
{
    private readonly ICategoryService _categoryService;

    public GetCategoriesQueryHandler(ICategoryService categoryService)
    {
        _categoryService = categoryService;
    }

    public async Task<List<CategoryResponseDto>> Handle(GetCategoriesQuery request, CancellationToken cancellationToken)
    {
        var categories = request.Type.HasValue
            ? await _categoryService.GetByTypeAsync(request.Type.Value)
            : await _categoryService.GetAllAsync();

        return categories.Select(CategoryResponseDto.FromEntity).ToList();
    }
}
