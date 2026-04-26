using FinanceTracker.Application.Dtos;
using FinanceTracker.Application.Features.Transactions.Commands.UpdateTransaction;
using FinanceTracker.Application.Services;
using FinanceTracker.Domain.Entities;
using FluentAssertions;
using Moq;

namespace FinanceTracker.Tests.Unit.Handlers;

public class UpdateTransactionCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenTransactionMissing_ReturnsNull()
    {
        var service = new Mock<ITransactionService>();
        service.Setup(s => s.UpdateTransactionAsync(It.IsAny<Guid>(), It.IsAny<UpdateTransactionDto>()))
            .ReturnsAsync((Transaction?)null);
        service.Setup(s => s.GetByIdAsync(It.IsAny<Guid>())).Throws(new InvalidOperationException("Should not load when update failed"));

        var sut = new UpdateTransactionCommandHandler(service.Object);
        var dto = new UpdateTransactionDto
        {
            Name = "x",
            CategoryId = Guid.NewGuid(),
            Amount = 1m,
            TransactionDate = DateTime.UtcNow
        };

        var result = await sut.Handle(new UpdateTransactionCommand(Guid.NewGuid(), dto), CancellationToken.None);

        result.Should().BeNull();
        service.Verify(s => s.GetByIdAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenUpdated_LoadsGraphForResponse()
    {
        var id = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var category = new Category
        {
            Id = categoryId,
            Name = "Travel",
            CategoryType = CategoryType.Expense,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        var dto = new UpdateTransactionDto
        {
            Name = "Train",
            CategoryId = categoryId,
            Description = "Return",
            Amount = 42m,
            TransactionDate = DateTime.UtcNow
        };

        var bareUpdate = new Transaction
        {
            Id = id,
            Name = dto.Name,
            CategoryId = categoryId,
            Category = null!,
            Description = dto.Description,
            Amount = dto.Amount,
            TransactionDate = dto.TransactionDate,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "user"
        };

        var service = new Mock<ITransactionService>();
        service.Setup(s => s.UpdateTransactionAsync(id, dto)).ReturnsAsync(bareUpdate);
        service.Setup(s => s.GetByIdAsync(id)).ReturnsAsync(new Transaction
        {
            Id = id,
            Name = dto.Name,
            CategoryId = categoryId,
            Category = category,
            Description = dto.Description,
            Amount = dto.Amount,
            TransactionDate = dto.TransactionDate,
            CreatedAt = bareUpdate.CreatedAt,
            CreatedBy = "user"
        });

        var sut = new UpdateTransactionCommandHandler(service.Object);

        var result = await sut.Handle(new UpdateTransactionCommand(id, dto), CancellationToken.None);

        result.Should().NotBeNull();
        result!.CategoryName.Should().Be("Travel");
        service.Verify(s => s.GetByIdAsync(id), Times.Once);
    }
}
