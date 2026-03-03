using FinanceTracker.Application.Dtos.Responses;
using MediatR;

namespace FinanceTracker.Application.Features.Frequencies.Queries.GetRecurringOptions;

public sealed record GetRecurringOptionsQuery : IRequest<List<FrequencyResponseDto>>;
