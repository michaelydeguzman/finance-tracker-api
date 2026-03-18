using FinanceTracker.Application.Dtos.Responses;
using FinanceTracker.Domain.Entities;
using MediatR;

namespace FinanceTracker.Application.Features.Transactions.Queries.GetAllTransactions;

public sealed record GetAllTransactionsQuery(
        CategoryType? CategoryType = null
    ) : IRequest<List<TransactionResponseDto>> ;
