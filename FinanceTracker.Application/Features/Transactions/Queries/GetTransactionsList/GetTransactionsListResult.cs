using FinanceTracker.Application.Dtos.Responses;

namespace FinanceTracker.Application.Features.Transactions.Queries.GetTransactionsList;

public sealed record GetTransactionsListResult(
    bool IsPaged,
    IReadOnlyList<TransactionResponseDto> Items,
    int? TotalCount);
