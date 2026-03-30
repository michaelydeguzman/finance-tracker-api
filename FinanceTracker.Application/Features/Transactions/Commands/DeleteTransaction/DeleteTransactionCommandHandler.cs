using FinanceTracker.Application.Services;
using MediatR;

namespace FinanceTracker.Application.Features.Transactions.Commands.DeleteTransaction;

public sealed class DeleteTransactionCommandHandler : IRequestHandler<DeleteTransactionCommand, bool>
{
    private readonly ITransactionService _transactionService;

    public DeleteTransactionCommandHandler(ITransactionService transactionService)
    {
        _transactionService = transactionService;
    }

    public async Task<bool> Handle(DeleteTransactionCommand request, CancellationToken cancellationToken)
    {
        return await _transactionService.DeleteTransactionAsync(request.Id);
    }
}
