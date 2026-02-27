using FinanceTracker.Application.Services;
using FinanceTracker.Domain.Entities;
using MediatR;

namespace FinanceTracker.Application.Features.Categories.Queries.GetCategories;

public sealed class GetCategoriesQueryHandler : IRequestHandler<GetCategoriesQuery, List<Category>>
{
    private readonly ICategoryService _categoryService;

    public GetCategoriesQueryHandler(ICategoryService categoryService)
    {
        _categoryService = categoryService;
    }

    public async Task<List<Category>> Handle(GetCategoriesQuery request, CancellationToken cancellationToken)
    {
        if (request.Type.HasValue)
        {
            return await _categoryService.GetByTypeAsync(request.Type.Value);
        }

        return await _categoryService.GetAllAsync();
    }
}
