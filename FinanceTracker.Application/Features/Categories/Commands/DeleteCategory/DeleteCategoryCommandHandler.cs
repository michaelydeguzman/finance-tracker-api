using FinanceTracker.Application.Services;
using MediatR;

namespace FinanceTracker.Application.Features.Categories.Commands.DeleteCategory;

public sealed class DeleteCategoryCommandHandler : IRequestHandler<DeleteCategoryCommand, CategoryCommandResult>
{
    private readonly ICategoryService _categoryService;

    public DeleteCategoryCommandHandler(ICategoryService categoryService)
    {
        _categoryService = categoryService;
    }

    public async Task<CategoryCommandResult> Handle(DeleteCategoryCommand request, CancellationToken cancellationToken)
    {
        return await _categoryService.DeleteCategoryAsync(request.Id, cancellationToken);
    }
}
