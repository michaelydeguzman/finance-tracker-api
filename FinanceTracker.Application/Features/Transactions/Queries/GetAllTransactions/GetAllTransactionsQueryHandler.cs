using FinanceTracker.Application.Dtos.Responses;
using FinanceTracker.Application.Services;
using MediatR;

namespace FinanceTracker.Application.Features.Transactions.Queries.GetAllTransactions;

public sealed class GetAllTransactionsQueryHandler : IRequestHandler<GetAllTransactionsQuery, List<TransactionResponseDto>>
{
    private readonly ITransactionService _transactionService;

    public GetAllTransactionsQueryHandler(ITransactionService transactionService)
    {
        _transactionService = transactionService;
    }

    public async Task<List<TransactionResponseDto>> Handle(GetAllTransactionsQuery request, CancellationToken cancellationToken)
    {
        if (request.CategoryType.HasValue)
        {
            var transactionsByCategoryType = await _transactionService.GetByCategoryType(request.CategoryType.Value);
            return transactionsByCategoryType.Select(TransactionResponseDto.FromEntity).ToList();
        }

        var transactions = await _transactionService.GetAllAsync();
        return transactions.Select(TransactionResponseDto.FromEntity).ToList();
    }
}
