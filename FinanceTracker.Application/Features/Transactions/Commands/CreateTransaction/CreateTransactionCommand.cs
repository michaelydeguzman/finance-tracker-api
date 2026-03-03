using FinanceTracker.Application.Dtos;
using FinanceTracker.Application.Dtos.Responses;
using MediatR;

namespace FinanceTracker.Application.Features.Transactions.Commands.CreateTransaction;

public sealed record CreateTransactionCommand(CreateTransactionDto Dto) : IRequest<TransactionResponseDto>;
