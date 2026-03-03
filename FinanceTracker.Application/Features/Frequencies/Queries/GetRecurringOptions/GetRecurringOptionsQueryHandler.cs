using FinanceTracker.Application.Dtos.Responses;
using FinanceTracker.Application.Services;
using MediatR;

namespace FinanceTracker.Application.Features.Frequencies.Queries.GetRecurringOptions;

public sealed class GetRecurringOptionsQueryHandler : IRequestHandler<GetRecurringOptionsQuery, List<FrequencyResponseDto>>
{
    private readonly IFrequencyService _frequencyService;

    public GetRecurringOptionsQueryHandler(IFrequencyService frequencyService)
    {
        _frequencyService = frequencyService;
    }

    public async Task<List<FrequencyResponseDto>> Handle(GetRecurringOptionsQuery request, CancellationToken cancellationToken)
    {
        var options = await _frequencyService.GetAllAsync();
        return options.Select(FrequencyResponseDto.FromEntity).ToList();
    }
}
