using FinanceTracker.Application.Dtos.Responses;
using FinanceTracker.Domain.Entities;
using FinanceTracker.Domain.Repositories;
using FinanceTracker.Domain.Services;
using MediatR;

namespace FinanceTracker.Application.Features.Households.Queries.GetMyHousehold;

public sealed class GetMyHouseholdQueryHandler : IRequestHandler<GetMyHouseholdQuery, HouseholdResponseDto?>
{
    private readonly IHouseholdRepository _households;
    private readonly ICurrentUserAccessor _currentUser;

    public GetMyHouseholdQueryHandler(IHouseholdRepository households, ICurrentUserAccessor currentUser)
    {
        _households = households;
        _currentUser = currentUser;
    }

    public async Task<HouseholdResponseDto?> Handle(GetMyHouseholdQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.RequireUserId();
        var household = await _households.GetForUserAsync(userId, cancellationToken);

        if (household is null)
            return null;

        var members = await _households.GetMembersAsync(household.Id, cancellationToken);

        // Only the owner sees who has been approached. A member can see who is *in* the
        // household — they are already sharing money with them — but the addresses the owner
        // has written to are not the same thing, and one of them may simply have said no.
        var pending = household.OwnerUserId != userId
            ? null
            : (await _households.GetInvitationsForHouseholdAsync(household.Id, cancellationToken))
                .Where(invitation => invitation.IsOpen(DateTime.UtcNow))
                .ToList();

        return HouseholdResponseDto.FromEntity(household, members, userId, pending);
    }
}
