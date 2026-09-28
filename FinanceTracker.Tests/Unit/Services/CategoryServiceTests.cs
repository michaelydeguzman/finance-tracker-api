using FinanceTracker.Application.Features.Categories;
using FinanceTracker.Application.Services;
using FinanceTracker.Domain.Entities;
using FinanceTracker.Domain.Repositories;
using FluentAssertions;
using Moq;

namespace FinanceTracker.Tests.Unit.Services;

/// <summary>
/// The name check runs before the save, so two identical writes can both pass it. The second
/// then reaches the unique index, and must come back as the same 409 rather than a 500.
/// </summary>
public class CategoryServiceTests
{
    private readonly Mock<ICategoryRepository> _repository = new();

    private static Category Groceries() => new()
    {
        Id = Guid.NewGuid(),
        Name = "Groceries",
        CategoryType = CategoryType.Expense,
        UserId = TestCurrentUserAccessor.DefaultUserId,
        IsActive = true
    };

    [Fact]
    public async Task AddCategory_WhenTheIndexRejectsAConcurrentDuplicate_IsAConflict()
    {
        _repository
            .Setup(r => r.NameTakenAsync(It.IsAny<Guid>(), It.IsAny<CategoryType>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _repository
            .Setup(r => r.AddAsync(It.IsAny<Category>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CategoryNameTakenException());

        var result = await new CategoryService(_repository.Object).AddCategoryAsync(Groceries());

        result.Outcome.Should().Be(CategoryOutcome.Conflict);
        result.Message.Should().Contain("Groceries");
    }

    [Fact]
    public async Task UpdateCategory_WhenTheIndexRejectsAConcurrentDuplicate_IsAConflict()
    {
        var category = Groceries();
        _repository
            .Setup(r => r.GetByIdAsync(category.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(category);
        _repository
            .Setup(r => r.NameTakenAsync(It.IsAny<Guid>(), It.IsAny<CategoryType>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _repository
            .Setup(r => r.UpdateAsync(It.IsAny<Category>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CategoryNameTakenException());

        var result = await new CategoryService(_repository.Object)
            .UpdateCategoryAsync(category.Id, "Food", CategoryType.Expense);

        result.Outcome.Should().Be(CategoryOutcome.Conflict);
        result.Message.Should().Contain("Food");
    }

    [Fact]
    public async Task DeleteCategory_WhenARecordLandsBetweenTheUsageCheckAndTheDelete_IsAConflict()
    {
        var category = Groceries();
        _repository
            .Setup(r => r.GetByIdAsync(category.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(category);
        _repository
            .Setup(r => r.GetUsageAsync(category.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CategoryUsage(0, 0));
        _repository
            .Setup(r => r.DeleteAsync(category.Id, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CategoryInUseException());

        var result = await new CategoryService(_repository.Object).DeleteCategoryAsync(category.Id);

        result.Outcome.Should().Be(CategoryOutcome.Conflict);
        result.Message.Should().Contain("Groceries");
    }
}
