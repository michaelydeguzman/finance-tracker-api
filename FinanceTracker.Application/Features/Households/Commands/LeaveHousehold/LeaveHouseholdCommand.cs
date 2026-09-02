using MediatR;

namespace FinanceTracker.Application.Features.Households.Commands.LeaveHousehold;

public sealed record LeaveHouseholdCommand : IRequest<HouseholdResult<object>>;
