namespace FinanceTracker.Application.Dtos.Responses;

public sealed record PagedTransactionsResponseDto(
    List<TransactionResponseDto> Items,
    int TotalCount);
