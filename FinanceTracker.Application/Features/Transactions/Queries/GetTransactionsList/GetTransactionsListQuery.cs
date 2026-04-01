using FinanceTracker.Domain.Entities;
using MediatR;

namespace FinanceTracker.Application.Features.Transactions.Queries.GetTransactionsList;

public sealed record GetTransactionsListQuery(
    CategoryType? CategoryType,
    DateTime? FromUtc,
    DateTime? ToUtc,
    IReadOnlyList<Guid>? CategoryIds,
    bool CategoryIdsParameterPresent,
    int? Page,
    int? PageSize
) : IRequest<GetTransactionsListResult>;
