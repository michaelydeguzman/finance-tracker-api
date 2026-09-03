using FinanceTracker.Domain.Entities;
using FinanceTracker.Domain.Repositories;
using FinanceTracker.Domain.Services;
using MediatR;

namespace FinanceTracker.Application.Features.Households.Commands.RevokeHouseholdInvitation;

public sealed class RevokeHouseholdInvitationCommandHandler
    : IRequestHandler<RevokeHouseholdInvitationCommand, HouseholdResult<object>>
{
    private readonly IHouseholdRepository _households;
    private readonly ICurrentUserAccessor _currentUser;

    public RevokeHouseholdInvitationCommandHandler(
        IHouseholdRepository households,
        ICurrentUserAccessor currentUser)
    {
        _households = households;
        _currentUser = currentUser;
    }

    public async Task<HouseholdResult<object>> Handle(
        RevokeHouseholdInvitationCommand request,
        CancellationToken cancellationToken)
    {
        var userId = _currentUser.RequireUserId();
        var household = await _households.GetForUserAsync(userId, cancellationToken);

        if (household is null)
            return HouseholdResult<object>.NotFound("You are not in a household.");

        if (household.OwnerUserId != userId)
            return HouseholdResult<object>.Forbidden("Only the household owner can revoke invitations.");

        var invitation = await _households.GetInvitationAsync(request.InvitationId, cancellationToken);

        // Invitations are the one table here that is deliberately readable across households,
        // so the ownership check is explicit: another household's invitation id answers 404,
        // exactly as an unknown one does.
        if (invitation is null || invitation.HouseholdId != household.Id)
            return HouseholdResult<object>.NotFound("Invitation not found.");

        if (invitation.Status != HouseholdInvitationStatus.Pending)
            return HouseholdResult<object>.Conflict("That invitation has already been answered.");

        invitation.Status = HouseholdInvitationStatus.Revoked;
        invitation.RespondedAt = DateTime.UtcNow;

        await _households.SaveChangesAsync(cancellationToken);

        return HouseholdResult<object>.Success(message: "Invitation revoked.");
    }
}
