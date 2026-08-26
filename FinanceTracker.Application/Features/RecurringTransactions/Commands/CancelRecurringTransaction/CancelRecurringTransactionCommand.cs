using MediatR;

namespace FinanceTracker.Application.Features.RecurringTransactions.Commands.CancelRecurringTransaction;

public sealed record CancelRecurringTransactionCommand(Guid Id) : IRequest<RecurringTransactionCommandResult>;
