using FinanceTracker.Application.Dtos.Responses;
using MediatR;

namespace FinanceTracker.Application.Features.Transactions.Queries.GetAllTransactions;

public sealed record GetAllTransactionsQuery : IRequest<List<TransactionResponseDto>>;
