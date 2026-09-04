using FinanceTracker.Application.Dtos;
using FinanceTracker.Application.Dtos.Responses;
using FinanceTracker.Application.Features.Transactions.Commands.DeleteTransaction;
using FinanceTracker.Application.Features.Transactions.Commands.UpdateTransaction;
using FinanceTracker.API.Controllers;
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

    /// <summary>
    /// The controller is where a live token enters the pipeline. Every handler below it
    /// forwards the token it is given, so an action that drops the request's own token
    /// silently turns that whole chain into CancellationToken.None and leaves queries
    /// running after the caller has disconnected.
    /// </summary>
    [Fact]
    public async Task DeleteTransaction_ForwardsTheRequestCancellationTokenToTheSender()
    {
        using var cts = new CancellationTokenSource();
        var sender = new Mock<ISender>();
        sender.Setup(s => s.Send(It.IsAny<DeleteTransactionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var sut = new TransactionsV1Controller(sender.Object);

        await sut.DeleteTransaction(Guid.NewGuid(), cts.Token);

        sender.Verify(
            s => s.Send(It.IsAny<DeleteTransactionCommand>(), cts.Token),
            Times.Once);
    }
}
