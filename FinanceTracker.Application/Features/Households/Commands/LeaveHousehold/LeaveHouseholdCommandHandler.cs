using FinanceTracker.Domain.Repositories;
using FinanceTracker.Domain.Services;
using MediatR;

namespace FinanceTracker.Application.Features.Households.Commands.LeaveHousehold;

public sealed class LeaveHouseholdCommandHandler
    : IRequestHandler<LeaveHouseholdCommand, HouseholdResult<object>>
{
    private readonly IHouseholdRepository _households;
    private readonly ICurrentUserAccessor _currentUser;

    public LeaveHouseholdCommandHandler(IHouseholdRepository households, ICurrentUserAccessor currentUser)
    {
        _households = households;
        _currentUser = currentUser;
    }

    public async Task<HouseholdResult<object>> Handle(
        LeaveHouseholdCommand request,
        CancellationToken cancellationToken)
    {
        var userId = _currentUser.RequireUserId();
        var household = await _households.GetForUserAsync(userId, cancellationToken);

        if (household is null)
            return HouseholdResult<object>.NotFound("You are not in a household.");

        var members = await _households.GetMembersAsync(household.Id, cancellationToken);
        var me = members.SingleOrDefault(m => m.Id == userId);

        if (me is null)
            return HouseholdResult<object>.NotFound("You are not in a household.");

        me.HouseholdId = null;
        await _households.ReassignRecordsAsync(userId, null, cancellationToken);

        var remaining = members.Where(m => m.Id != userId).ToList();

        if (remaining.Count == 0)
        {
            // The last person out closes it. Leaving an empty household behind would leave a
            // row nobody can reach, still holding open invitations people could accept into
            // a group with no members.
            await _households.RemoveAsync(household, cancellationToken);
            await _households.SaveChangesAsync(cancellationToken);

            return HouseholdResult<object>.Success(message: "You have left the household, and it has been closed.");
        }

        if (household.OwnerUserId == userId)
        {
            // Ownership passes to whoever has been in the household longest rather than
            // blocking the owner from leaving. The alternative — refusing until they have
            // removed everyone else — makes the only way out of a household you own the
            // removal of people whose records are not yours to decide about.
            var successor = remaining
                .OrderBy(m => m.CreatedAt)
                .ThenBy(m => m.Email, StringComparer.Ordinal)
                .First();

            household.OwnerUserId = successor.Id;
        }

        await _households.SaveChangesAsync(cancellationToken);

        return HouseholdResult<object>.Success(message: "You have left the household.");
    }
}
