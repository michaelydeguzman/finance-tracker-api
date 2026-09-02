using FinanceTracker.Application.Dtos.Responses;
using MediatR;

namespace FinanceTracker.Application.Features.Households.Commands.RemoveHouseholdMember;

public sealed record RemoveHouseholdMemberCommand(Guid MemberUserId)
    : IRequest<HouseholdResult<HouseholdResponseDto>>;
