using FinanceTracker.Application.Dtos;
using FinanceTracker.Application.Dtos.Responses;
using MediatR;

namespace FinanceTracker.Application.Features.Transactions.Commands.UpdateTransaction;

public sealed record UpdateTransactionCommand(Guid Id, UpdateTransactionDto Dto) : IRequest<TransactionResponseDto?>;
