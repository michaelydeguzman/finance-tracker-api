using FinanceTracker.Application.Dtos.Responses;
using FinanceTracker.Domain.Repositories;
using FinanceTracker.Domain.Services;
using MediatR;

namespace FinanceTracker.Application.Features.Households.Queries.GetMyInvitations;

public sealed class GetMyInvitationsQueryHandler
    : IRequestHandler<GetMyInvitationsQuery, List<HouseholdInvitationDto>>
{
    private readonly IHouseholdRepository _households;
    private readonly IUserRepository _users;
    private readonly ICurrentUserAccessor _currentUser;

    public GetMyInvitationsQueryHandler(
        IHouseholdRepository households,
        IUserRepository users,
        ICurrentUserAccessor currentUser)
    {
        _households = households;
        _users = users;
        _currentUser = currentUser;
    }

    public async Task<List<HouseholdInvitationDto>> Handle(
        GetMyInvitationsQuery request,
        CancellationToken cancellationToken)
    {
        var userId = _currentUser.RequireUserId();

        // The address is read from the record, not from the access token's email claim. A
        // token outlives a change of address, and the wrong address here does not fail
        // loudly — it silently shows the wrong person's invitations.
        var user = await _users.GetByIdAsync(userId, cancellationToken);

        if (user is null)
            return [];

        var invitations = await _households.GetOpenInvitationsForEmailAsync(
            user.Email, DateTime.UtcNow, cancellationToken);

        return invitations
            .Select(invitation => HouseholdInvitationDto.FromEntity(
                invitation, invitation.Household?.Name ?? string.Empty))
            .ToList();
    }
}
