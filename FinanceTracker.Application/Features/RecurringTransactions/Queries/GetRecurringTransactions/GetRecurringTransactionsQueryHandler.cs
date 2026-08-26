using FinanceTracker.Application.Dtos.Responses;
using FinanceTracker.Infrastructure.Persistence;
using MediatR;

namespace FinanceTracker.Application.Features.RecurringTransactions.Queries.GetRecurringTransactions;

public sealed class GetRecurringTransactionsQueryHandler
    : IRequestHandler<GetRecurringTransactionsQuery, List<RecurringTransactionResponseDto>>
{
    private readonly IRecurringTransactionRepository _templates;

    public GetRecurringTransactionsQueryHandler(IRecurringTransactionRepository templates)
        => _templates = templates;

    public async Task<List<RecurringTransactionResponseDto>> Handle(
        GetRecurringTransactionsQuery request,
        CancellationToken cancellationToken)
    {
        var templates = await _templates.GetAllAsync(request.Status);
        return templates.Select(RecurringTransactionResponseDto.FromEntity).ToList();
    }
}
