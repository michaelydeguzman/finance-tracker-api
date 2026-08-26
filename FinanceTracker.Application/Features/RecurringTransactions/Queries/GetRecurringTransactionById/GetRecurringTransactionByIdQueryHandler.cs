using FinanceTracker.Application.Dtos.Responses;
using FinanceTracker.Infrastructure.Persistence;
using MediatR;

namespace FinanceTracker.Application.Features.RecurringTransactions.Queries.GetRecurringTransactionById;

public sealed class GetRecurringTransactionByIdQueryHandler
    : IRequestHandler<GetRecurringTransactionByIdQuery, RecurringTransactionResponseDto?>
{
    private readonly IRecurringTransactionRepository _templates;

    public GetRecurringTransactionByIdQueryHandler(IRecurringTransactionRepository templates)
        => _templates = templates;

    public async Task<RecurringTransactionResponseDto?> Handle(
        GetRecurringTransactionByIdQuery request,
        CancellationToken cancellationToken)
    {
        // Another tenant's id comes back null from the filtered read, so the controller's
        // 404 is the same answer for "does not exist" and "is not yours".
        var template = await _templates.GetByIdAsync(request.Id);
        return template is null ? null : RecurringTransactionResponseDto.FromEntity(template);
    }
}
