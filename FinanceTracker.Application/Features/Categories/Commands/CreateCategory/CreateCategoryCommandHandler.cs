using FinanceTracker.Domain.Services;
using FinanceTracker.Application.Dtos.Responses;
using FinanceTracker.Application.Services;
using FinanceTracker.Domain.Entities;
using MediatR;

namespace FinanceTracker.Application.Features.Categories.Commands.CreateCategory;

public sealed class CreateCategoryCommandHandler : IRequestHandler<CreateCategoryCommand, CategoryResponseDto>
{
    private readonly ICategoryService _categoryService;
    private readonly ICurrentUserAccessor _currentUser;

    public CreateCategoryCommandHandler(ICategoryService categoryService, ICurrentUserAccessor currentUser)
    {
        _categoryService = categoryService;
        _currentUser = currentUser;
    }

    public async Task<CategoryResponseDto> Handle(CreateCategoryCommand request, CancellationToken cancellationToken)
    {
        var category = new Category
        {
            Id = Guid.NewGuid(),
            Name = request.Dto.Name,
            CategoryType = request.Dto.CategoryType,
            UserId = _currentUser.RequireUserId(),
            // Stamped from the writer's membership at the moment of writing, which is what
            // makes the household half of the tenancy filter a scalar compare. Null when they
            // are on their own, and the row stays private until they join.
            HouseholdId = _currentUser.HouseholdId,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        var created = await _categoryService.AddCategoryAsync(category, cancellationToken);
        return CategoryResponseDto.FromEntity(created);
    }
}
