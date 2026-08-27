using MediatR;

namespace FinanceTracker.Application.Features.RecurringTransactions.Commands.ResumeRecurringTransaction;

public sealed record ResumeRecurringTransactionCommand(Guid Id) : IRequest<RecurringTransactionCommandResult>;
