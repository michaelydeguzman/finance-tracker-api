using FinanceTracker.Application.Dtos.Responses;
using FinanceTracker.Domain.Repositories;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Application.Features.Transactions.Queries.GetTransactionsList;

public sealed class GetTransactionsListQueryHandler : IRequestHandler<GetTransactionsListQuery, GetTransactionsListResult>
{
    private readonly ITransactionRepository _transactionRepository;

    public GetTransactionsListQueryHandler(ITransactionRepository transactionRepository)
    {
        _transactionRepository = transactionRepository;
    }

    public async Task<GetTransactionsListResult> Handle(GetTransactionsListQuery request, CancellationToken cancellationToken)
    {
        var query = _transactionRepository.GetTransactionsQueryable();

        if (request.FromUtc is { } from)
            query = query.Where(t => t.TransactionDate >= from);

        if (request.ToUtc is { } to)
            query = query.Where(t => t.TransactionDate <= to);

        if (request.CategoryIds is { Count: > 0 })
            query = query.Where(t => request.CategoryIds.Contains(t.CategoryId));

        if (request.CategoryType is { } categoryType)
            query = query.Where(t => t.Category.CategoryType == categoryType);

        var isPaged = request.Page is not null && request.PageSize is not null;

        if (!isPaged)
        {
            query = query.OrderByDescending(t => t.CreatedAt);
            var list = await query.ToListAsync(cancellationToken);
            var unpagedItems = list.Select(TransactionResponseDto.FromEntity).ToList();
            return new GetTransactionsListResult(false, unpagedItems, null);
        }

        query = query.OrderByDescending(t => t.TransactionDate).ThenByDescending(t => t.Id);
        var total = await query.CountAsync(cancellationToken);
        var page = request.Page!.Value;
        var size = request.PageSize!.Value;
        var items = await query
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(cancellationToken);

        var pagedItems = items.Select(TransactionResponseDto.FromEntity).ToList();
        return new GetTransactionsListResult(true, pagedItems, total);
    }
}
