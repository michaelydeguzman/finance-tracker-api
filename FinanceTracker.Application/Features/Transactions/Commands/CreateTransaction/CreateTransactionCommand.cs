using FinanceTracker.Application.Dtos;
using FinanceTracker.Application.Dtos.Responses;
using MediatR;

namespace FinanceTracker.Application.Features.Transactions.Commands.CreateTransaction;

/// <summary>
/// Null when the category cannot be reached, which is the same answer the recurring create
/// command already gives: a transaction whose category the caller cannot see would be
/// invisible to everyone, because that navigation is required and the category is filtered.
/// </summary>
public sealed record CreateTransactionCommand(CreateTransactionDto Dto) : IRequest<TransactionResponseDto?>;
