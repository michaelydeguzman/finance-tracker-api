using FinanceTracker.Application.Dtos.Households;
using FinanceTracker.Application.Dtos.Responses;
using MediatR;

namespace FinanceTracker.Application.Features.Households.Commands.RenameHousehold;

public sealed record RenameHouseholdCommand(RenameHouseholdDto Dto)
    : IRequest<HouseholdResult<HouseholdResponseDto>>;
