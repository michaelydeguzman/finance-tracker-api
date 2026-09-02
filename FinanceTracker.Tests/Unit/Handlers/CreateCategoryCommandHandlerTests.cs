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
            .Setup(s => s.AddCategoryAsync(It.IsAny<Category>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Category c, CancellationToken _) => c);

        var sut = new CreateCategoryCommandHandler(categoryService.Object, new TestCurrentUserAccessor());

        var result = await sut.Handle(new CreateCategoryCommand(dto), CancellationToken.None);

        result.Name.Should().Be("Utilities");
        result.CategoryType.Should().Be(CategoryType.Expense);
        categoryService.Verify(s => s.AddCategoryAsync(It.Is<Category>(c =>
            c.Name == dto.Name && c.CategoryType == dto.CategoryType && c.IsActive
            && c.UserId == TestCurrentUserAccessor.DefaultUserId), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Handle_StampsTheWritersHouseholdOntoTheCategory(bool inAHousehold)
    {
        // The stamp is what the widened tenancy filter matches on, so a category written
        // without it is invisible to the household that is supposed to share it — and one
        // written with a household the writer is not in would be visible to strangers.
        var household = inAHousehold ? Guid.NewGuid() : (Guid?)null;

        var categoryService = new Mock<ICategoryService>();
        categoryService
            .Setup(s => s.AddCategoryAsync(It.IsAny<Category>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Category c, CancellationToken _) => c);

        var sut = new CreateCategoryCommandHandler(
            categoryService.Object,
            new TestCurrentUserAccessor(TestCurrentUserAccessor.DefaultUserId, household));

        await sut.Handle(
            new CreateCategoryCommand(new CreateCategoryDto { Name = "Utilities", CategoryType = CategoryType.Expense }),
            CancellationToken.None);

        categoryService.Verify(
            s => s.AddCategoryAsync(
                It.Is<Category>(c => c.HouseholdId == household), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
