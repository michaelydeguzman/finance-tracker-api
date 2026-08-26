using MediatR;

namespace FinanceTracker.Application.Features.RecurringTransactions.Commands.DeleteRecurringTransaction;

public sealed record DeleteRecurringTransactionCommand(Guid Id) : IRequest<RecurringTransactionCommandResult>;
