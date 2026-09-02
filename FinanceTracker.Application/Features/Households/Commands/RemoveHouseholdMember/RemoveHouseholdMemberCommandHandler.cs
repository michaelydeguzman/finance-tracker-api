using FinanceTracker.Application.Dtos.Responses;
using FinanceTracker.Domain.Repositories;
using FinanceTracker.Domain.Services;
using MediatR;

namespace FinanceTracker.Application.Features.Households.Commands.RemoveHouseholdMember;

public sealed class RemoveHouseholdMemberCommandHandler
    : IRequestHandler<RemoveHouseholdMemberCommand, HouseholdResult<HouseholdResponseDto>>
{
    private readonly IHouseholdRepository _households;
    private readonly ICurrentUserAccessor _currentUser;

    public RemoveHouseholdMemberCommandHandler(
        IHouseholdRepository households,
        ICurrentUserAccessor currentUser)
    {
        _households = households;
        _currentUser = currentUser;
    }

    public async Task<HouseholdResult<HouseholdResponseDto>> Handle(
        RemoveHouseholdMemberCommand request,
        CancellationToken cancellationToken)
    {
        var userId = _currentUser.RequireUserId();
        var household = await _households.GetForUserAsync(userId, cancellationToken);

        if (household is null)
            return HouseholdResult<HouseholdResponseDto>.NotFound("You are not in a household.");

        if (household.OwnerUserId != userId)
            return HouseholdResult<HouseholdResponseDto>.Forbidden("Only the household owner can remove people.");

        if (request.MemberUserId == userId)
        {
            // Removing yourself is leaving, and leaving has to decide what becomes of the
            // household you own. Sending it there keeps that decision in one place.
            return HouseholdResult<HouseholdResponseDto>.Invalid(
                "To remove yourself, leave the household instead.");
        }

        var members = await _households.GetMembersAsync(household.Id, cancellationToken);
        var member = members.SingleOrDefault(m => m.Id == request.MemberUserId);

        if (member is null)
            return HouseholdResult<HouseholdResponseDto>.NotFound("That person is not in this household.");

        member.HouseholdId = null;

        // Their records go back to being theirs alone. Leaving them stamped would keep the
        // household reading a former member's finances for as long as the rows existed.
        await _households.ReassignRecordsAsync(member.Id, null, cancellationToken);

        await _households.SaveChangesAsync(cancellationToken);

        var remaining = await _households.GetMembersAsync(household.Id, cancellationToken);

        return HouseholdResult<HouseholdResponseDto>.Success(
            HouseholdResponseDto.FromEntity(household, remaining, userId));
    }
}
