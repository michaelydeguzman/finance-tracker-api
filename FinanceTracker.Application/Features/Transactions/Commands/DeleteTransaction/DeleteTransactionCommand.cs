using MediatR;

namespace FinanceTracker.Application.Features.Transactions.Commands.DeleteTransaction;

public sealed record DeleteTransactionCommand(Guid Id) : IRequest<bool>;
