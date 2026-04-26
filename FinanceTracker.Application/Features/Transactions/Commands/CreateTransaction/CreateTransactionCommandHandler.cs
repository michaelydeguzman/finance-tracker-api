using FinanceTracker.Application.Dtos.Responses;
using FinanceTracker.Application.Services;
using FinanceTracker.Domain.Entities;
using MediatR;

namespace FinanceTracker.Application.Features.Transactions.Commands.CreateTransaction;

public sealed class CreateTransactionCommandHandler : IRequestHandler<CreateTransactionCommand, TransactionResponseDto>
{
    private readonly ITransactionService _transactionService;

    public CreateTransactionCommandHandler(ITransactionService transactionService)
    {
        _transactionService = transactionService;
    }

    public async Task<TransactionResponseDto> Handle(CreateTransactionCommand request, CancellationToken cancellationToken)
    {
        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            Name = request.Dto.Name,
            CategoryId = request.Dto.CategoryId,
            Category = null!,
            Description = request.Dto.Description ?? string.Empty,
            Amount = request.Dto.Amount,
            TransactionDate = request.Dto.TransactionDate,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = request.Dto.CreatedBy
        };

        var created = await _transactionService.AddTransactionAsync(transaction);
        var createdWithRelations = await _transactionService.GetByIdAsync(created.Id);

        return TransactionResponseDto.FromEntity(createdWithRelations ?? created);
    }
}
