using FinanceTracker.Application.Features.Transactions.Commands.DeleteTransaction;
using FinanceTracker.Application.Services;
using FluentAssertions;
using Moq;

namespace FinanceTracker.Tests.Unit.Handlers;

public class DeleteTransactionCommandHandlerTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Handle_DelegatesToService(bool deleted)
    {
        var id = Guid.NewGuid();
        var service = new Mock<ITransactionService>();
        service.Setup(s => s.DeleteTransactionAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(deleted);

        var sut = new DeleteTransactionCommandHandler(service.Object);

        var result = await sut.Handle(new DeleteTransactionCommand(id), CancellationToken.None);

        result.Should().Be(deleted);
        service.Verify(s => s.DeleteTransactionAsync(id, It.IsAny<CancellationToken>()), Times.Once);
    }
}
