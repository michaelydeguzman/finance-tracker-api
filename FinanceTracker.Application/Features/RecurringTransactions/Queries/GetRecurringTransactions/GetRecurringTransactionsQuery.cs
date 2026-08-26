using FinanceTracker.Domain.Entities;
using MediatR;

namespace FinanceTracker.Application.Features.RecurringTransactions.Queries.GetRecurringTransactions;

/// <summary>
/// The caller's templates. There is no user parameter: scoping comes from the token by way
/// of the model-level query filter, so there is nothing here for a caller to tamper with.
/// </summary>
public sealed record GetRecurringTransactionsQuery(RecurringTransactionStatus? Status)
    : IRequest<List<Dtos.Responses.RecurringTransactionResponseDto>>;
