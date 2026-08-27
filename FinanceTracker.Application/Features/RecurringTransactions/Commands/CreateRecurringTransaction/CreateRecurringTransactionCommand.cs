using FinanceTracker.Application.Dtos;
using MediatR;

namespace FinanceTracker.Application.Features.RecurringTransactions.Commands.CreateRecurringTransaction;

public sealed record CreateRecurringTransactionCommand(CreateRecurringTransactionDto Dto)
    : IRequest<RecurringTransactionCommandResult>;
