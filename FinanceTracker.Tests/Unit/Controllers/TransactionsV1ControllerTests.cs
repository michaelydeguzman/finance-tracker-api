using FinanceTracker.Application.Dtos;
using FinanceTracker.Application.Dtos.Responses;
using FinanceTracker.Application.Features.Transactions.Commands.DeleteTransaction;
using FinanceTracker.Application.Features.Transactions.Commands.UpdateTransaction;
using FinanceTracker.Controllers;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace FinanceTracker.Tests.Unit.Controllers;

/// <summary>
/// API surface tests with MediatR mocked — common pattern for thin controllers.
/// </summary>
public class TransactionsV1ControllerTests
{
    [Fact]
    public async Task UpdateTransaction_WhenNotFound_ReturnsNotFoundObjectResult()
    {
        var id = Guid.NewGuid();
        var dto = new UpdateTransactionDto
        {
            Name = "x",
            CategoryId = Guid.NewGuid(),
            Amount = 1m,
            TransactionDate = DateTime.UtcNow
        };

        var sender = new Mock<ISender>();
        sender.Setup(s => s.Send(It.IsAny<UpdateTransactionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TransactionResponseDto?)null);

        var sut = new TransactionsV1Controller(sender.Object);
        sut.ModelState.Clear();

        var result = await sut.UpdateTransaction(id, dto);

        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task DeleteTransaction_WhenNotFound_ReturnsNotFoundObjectResult()
    {
        var id = Guid.NewGuid();
        var sender = new Mock<ISender>();
        sender.Setup(s => s.Send(It.IsAny<DeleteTransactionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var sut = new TransactionsV1Controller(sender.Object);

        var result = await sut.DeleteTransaction(id);

        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }
}
