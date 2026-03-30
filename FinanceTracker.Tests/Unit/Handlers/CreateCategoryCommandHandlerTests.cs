using FinanceTracker.Application.Dtos;
using FinanceTracker.Application.Features.Categories.Commands.CreateCategory;
using FinanceTracker.Application.Services;
using FinanceTracker.Domain.Entities;
using FluentAssertions;
using Moq;

namespace FinanceTracker.Tests.Unit.Handlers;

public class CreateCategoryCommandHandlerTests
{
    [Fact]
    public async Task Handle_PersistsCategoryAndReturnsDto()
    {
        var dto = new CreateCategoryDto { Name = "Utilities", CategoryType = CategoryType.Expense };

        var categoryService = new Mock<ICategoryService>();
        categoryService
            .Setup(s => s.AddCategoryAsync(It.IsAny<Category>()))
            .ReturnsAsync((Category c) => c);

        var sut = new CreateCategoryCommandHandler(categoryService.Object);

        var result = await sut.Handle(new CreateCategoryCommand(dto), CancellationToken.None);

        result.Name.Should().Be("Utilities");
        result.CategoryType.Should().Be(CategoryType.Expense);
        categoryService.Verify(s => s.AddCategoryAsync(It.Is<Category>(c =>
            c.Name == dto.Name && c.CategoryType == dto.CategoryType && c.IsActive)), Times.Once);
    }
}
