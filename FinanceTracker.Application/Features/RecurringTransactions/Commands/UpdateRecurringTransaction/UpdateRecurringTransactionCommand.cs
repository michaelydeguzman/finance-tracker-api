using FinanceTracker.Application.Dtos;
using MediatR;

namespace FinanceTracker.Application.Features.RecurringTransactions.Commands.UpdateRecurringTransaction;

public sealed record UpdateRecurringTransactionCommand(Guid Id, UpdateRecurringTransactionDto Dto)
    : IRequest<RecurringTransactionCommandResult>;
