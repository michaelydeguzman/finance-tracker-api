using FinanceTracker.Application.Dtos.Households;
using FinanceTracker.Application.Dtos.Responses;
using MediatR;

namespace FinanceTracker.Application.Features.Households.Commands.CreateHousehold;

public sealed record CreateHouseholdCommand(CreateHouseholdDto Dto)
    : IRequest<HouseholdResult<HouseholdResponseDto>>;
