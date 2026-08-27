using MediatR;

namespace FinanceTracker.Application.Features.RecurringTransactions.Commands.PauseRecurringTransaction;

public sealed record PauseRecurringTransactionCommand(Guid Id) : IRequest<RecurringTransactionCommandResult>;
