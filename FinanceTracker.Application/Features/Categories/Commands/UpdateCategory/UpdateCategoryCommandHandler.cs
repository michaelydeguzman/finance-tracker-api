using FinanceTracker.Application.Services;
using MediatR;

namespace FinanceTracker.Application.Features.Categories.Commands.UpdateCategory;

public sealed class UpdateCategoryCommandHandler : IRequestHandler<UpdateCategoryCommand, CategoryCommandResult>
{
    private readonly ICategoryService _categoryService;

    public UpdateCategoryCommandHandler(ICategoryService categoryService)
    {
        _categoryService = categoryService;
    }

    public async Task<CategoryCommandResult> Handle(UpdateCategoryCommand request, CancellationToken cancellationToken)
    {
        return await _categoryService.UpdateCategoryAsync(request.Id, request.Dto.Name, request.Dto.CategoryType, cancellationToken);
    }
}
