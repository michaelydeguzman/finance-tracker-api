using FinanceTracker.Application.Dtos.Households;
using FinanceTracker.Application.Dtos.Responses;
using MediatR;

namespace FinanceTracker.Application.Features.Households.Commands.InviteHouseholdMember;

public sealed record InviteHouseholdMemberCommand(InviteHouseholdMemberDto Dto)
    : IRequest<HouseholdResult<HouseholdInvitationDto>>;
