using FinanceTracker.Application.Dtos;
using FinanceTracker.Application.Dtos.Responses;
using FinanceTracker.Application.Features.Transactions.Commands.CreateTransaction;
using FinanceTracker.Application.Services;
using FinanceTracker.Domain.Entities;
using FluentAssertions;
using Moq;

namespace FinanceTracker.Tests.Unit.Handlers;

public class CreateTransactionCommandHandlerTests
{
    [Fact]
    public async Task Handle_AddsTransactionAndMapsResponse()
    {
        var categoryId = Guid.NewGuid();
        var category = new Category
        {
            Id = categoryId,
            Name = "Food",
            CategoryType = CategoryType.Expense,
            UserId = TestCurrentUserAccessor.DefaultUserId,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        var dto = new CreateTransactionDto
        {
            Name = "Coffee",
            CategoryId = categoryId,
            Description = "Morning",
            Amount = 3.50m,
            TransactionDate = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc)
        };

        var service = new Mock<ITransactionService>(MockBehavior.Strict);
        service
            .Setup(s => s.AddTransactionAsync(It.IsAny<Transaction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Transaction t) => t);
        service
            .Setup(s => s.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id) => new Transaction
            {
                Id = id,
                Name = dto.Name,
                CategoryId = categoryId,
                Category = category,
                UserId = TestCurrentUserAccessor.DefaultUserId,
                Description = dto.Description!,
                Amount = dto.Amount,
                TransactionDate = dto.TransactionDate,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = TestCurrentUserAccessor.DefaultEmail
            });

        var sut = new CreateTransactionCommandHandler(service.Object, new TestCurrentUserAccessor());

        var result = await sut.Handle(new CreateTransactionCommand(dto), CancellationToken.None);

        result.Should().BeOfType<TransactionResponseDto>();
        result.Name.Should().Be("Coffee");
        result.CategoryName.Should().Be("Food");
        result.CategoryType.Should().Be(CategoryType.Expense.ToString());
        service.Verify(s => s.AddTransactionAsync(It.Is<Transaction>(t =>
            t.Name == dto.Name && t.CategoryId == categoryId && t.CreatedBy == TestCurrentUserAccessor.DefaultEmail),
            It.IsAny<CancellationToken>()), Times.Once);
        service.Verify(s => s.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
