using FinanceTracker.Domain.Services;
using FinanceTracker.Application.Dtos.Responses;
using FinanceTracker.Application.Services;
using FinanceTracker.Domain.Entities;
using FinanceTracker.Domain.Repositories;
using MediatR;

namespace FinanceTracker.Application.Features.Transactions.Commands.CreateTransaction;

public sealed class CreateTransactionCommandHandler : IRequestHandler<CreateTransactionCommand, TransactionResponseDto?>
{
    private readonly ITransactionService _transactionService;
    private readonly ICategoryRepository _categories;
    private readonly ICurrentUserAccessor _currentUser;

    public CreateTransactionCommandHandler(
        ITransactionService transactionService,
        ICategoryRepository categories,
        ICurrentUserAccessor currentUser)
    {
        _transactionService = transactionService;
        _categories = categories;
        _currentUser = currentUser;
    }

    public async Task<TransactionResponseDto?> Handle(CreateTransactionCommand request, CancellationToken cancellationToken)
    {
        // Same reachability check the recurring create handler applies: an unreachable
        // category id would produce a row that no member can see, since a Transaction's
        // Category is a required navigation to a filtered entity.
        var category = await _categories.GetByIdAsync(request.Dto.CategoryId, cancellationToken);

        if (category is null)
            return null;

        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            Name = request.Dto.Name,
            CategoryId = request.Dto.CategoryId,
            Category = null!,
            UserId = _currentUser.RequireUserId(),
            // Stamped from the writer's membership at the moment of writing, which is what
            // makes the household half of the tenancy filter a scalar compare. Null when they
            // are on their own, and the row stays private until they join.
            HouseholdId = _currentUser.HouseholdId,
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
