using FinanceTracker.Domain.Services;
using FinanceTracker.Application.Dtos.Responses;
using FinanceTracker.Application.Services;
using FinanceTracker.Domain.Entities;
using MediatR;

namespace FinanceTracker.Application.Features.Transactions.Commands.CreateTransaction;

public sealed class CreateTransactionCommandHandler : IRequestHandler<CreateTransactionCommand, TransactionResponseDto>
{
    private readonly ITransactionService _transactionService;
    private readonly ICurrentUserAccessor _currentUser;

    public CreateTransactionCommandHandler(ITransactionService transactionService, ICurrentUserAccessor currentUser)
    {
        _transactionService = transactionService;
        _currentUser = currentUser;
    }

    public async Task<TransactionResponseDto> Handle(CreateTransactionCommand request, CancellationToken cancellationToken)
    {
        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            Name = request.Dto.Name,
            CategoryId = request.Dto.CategoryId,
            Category = null!,
            UserId = _currentUser.RequireUserId(),
            Description = request.Dto.Description ?? string.Empty,
            Amount = request.Dto.Amount,
            TransactionDate = request.Dto.TransactionDate,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = _currentUser.Email ?? _currentUser.RequireUserId().ToString()
        };

        var created = await _transactionService.AddTransactionAsync(transaction, cancellationToken);
        var createdWithRelations = await _transactionService.GetByIdAsync(created.Id, cancellationToken);

        return TransactionResponseDto.FromEntity(createdWithRelations ?? created);
    }
}
