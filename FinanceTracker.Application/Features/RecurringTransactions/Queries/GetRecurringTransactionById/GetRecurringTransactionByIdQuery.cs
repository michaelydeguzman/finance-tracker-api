using FinanceTracker.Application.Dtos.Responses;
using MediatR;

namespace FinanceTracker.Application.Features.RecurringTransactions.Queries.GetRecurringTransactionById;

public sealed record GetRecurringTransactionByIdQuery(Guid Id) : IRequest<RecurringTransactionResponseDto?>;
