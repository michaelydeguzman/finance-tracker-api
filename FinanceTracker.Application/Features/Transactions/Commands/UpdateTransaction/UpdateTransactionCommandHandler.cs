using FinanceTracker.Application.Dtos.Responses;
using FinanceTracker.Application.Services;
using MediatR;

namespace FinanceTracker.Application.Features.Transactions.Commands.UpdateTransaction;

public sealed class UpdateTransactionCommandHandler : IRequestHandler<UpdateTransactionCommand, TransactionResponseDto?>
{
    private readonly ITransactionService _transactionService;

    public UpdateTransactionCommandHandler(ITransactionService transactionService)
    {
        _transactionService = transactionService;
    }

    public async Task<TransactionResponseDto?> Handle(UpdateTransactionCommand request, CancellationToken cancellationToken)
    {
        var updated = await _transactionService.UpdateTransactionAsync(request.Id, request.Dto);
        if (updated is null)
            return null;

        var withRelations = await _transactionService.GetByIdAsync(request.Id);
        return TransactionResponseDto.FromEntity(withRelations ?? updated);
    }
}
