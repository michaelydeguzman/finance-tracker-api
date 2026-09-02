using FinanceTracker.Application.Dtos.Responses;
using FinanceTracker.Domain.Entities;
using FinanceTracker.Domain.Repositories;
using FinanceTracker.Domain.Services;
using MediatR;

namespace FinanceTracker.Application.Features.Households.Commands.RespondToHouseholdInvitation;

public sealed class RespondToHouseholdInvitationCommandHandler
    : IRequestHandler<RespondToHouseholdInvitationCommand, HouseholdResult<HouseholdResponseDto>>
{
    private readonly IHouseholdRepository _households;
    private readonly IUserRepository _users;
    private readonly ICurrentUserAccessor _currentUser;

    public RespondToHouseholdInvitationCommandHandler(
        IHouseholdRepository households,
        IUserRepository users,
        ICurrentUserAccessor currentUser)
    {
        _households = households;
        _users = users;
        _currentUser = currentUser;
    }

    public async Task<HouseholdResult<HouseholdResponseDto>> Handle(
        RespondToHouseholdInvitationCommand request,
        CancellationToken cancellationToken)
    {
        var userId = _currentUser.RequireUserId();
        var user = await _users.GetByIdAsync(userId, cancellationToken);

        if (user is null)
            return HouseholdResult<HouseholdResponseDto>.NotFound("Your account could not be found.");

        var invitation = await _households.GetInvitationAsync(request.InvitationId, cancellationToken);

        // Addressed to someone else, or not there at all — one answer for both. An invitation
        // id that told a stranger "that exists, but it is not yours" would confirm that the
        // address it names has been approached.
        if (invitation is null || invitation.InvitedEmail != user.Email)
            return HouseholdResult<HouseholdResponseDto>.NotFound("Invitation not found.");

        var now = DateTime.UtcNow;

        if (!invitation.IsOpen(now))
            return HouseholdResult<HouseholdResponseDto>.Conflict("That invitation is no longer valid.");

        if (!request.Accept)
        {
            invitation.Status = HouseholdInvitationStatus.Declined;
            invitation.RespondedAt = now;
            await _households.SaveChangesAsync(cancellationToken);

            return HouseholdResult<HouseholdResponseDto>.Success(message: "Invitation declined.");
        }

        // Accepting publishes this person's whole financial history to everyone already in
        // the household. An address nobody has proved control of must not be able to do that:
        // otherwise inviting a typo'd address is enough to hand a stranger's records over to
        // whoever registers it next.
        if (user.EmailVerifiedAt is null)
        {
            return HouseholdResult<HouseholdResponseDto>.Invalid(
                "Confirm your email address before joining a household.");
        }

        if (user.HouseholdId is not null)
        {
            return HouseholdResult<HouseholdResponseDto>.Conflict(
                "You are already in a household. Leave it before joining another.");
        }

        var household = await _households.GetByIdAsync(invitation.HouseholdId, cancellationToken);

        if (household is null)
            return HouseholdResult<HouseholdResponseDto>.NotFound("That household no longer exists.");

        // An invitation is an offer from a person, not a standing property of the household.
        // Without this, an offer outlives its author: A invites B, A leaves, ownership passes
        // to C, and B's acceptance days later publishes B's entire history to C — someone B
        // has never heard of, on the strength of an offer from someone who has gone. The
        // invitation DTO only ever showed B the household's name, so nothing warned them.
        var membersBefore = await _households.GetMembersAsync(household.Id, cancellationToken);

        if (membersBefore.All(member => member.Id != invitation.InvitedByUserId))
        {
            // Retired on the spot, not merely refused. It is dead either way, and leaving it
            // Pending makes its own advice impossible to follow: HasOpenInvitationAsync would
            // still see it, so the re-invitation this message asks for comes back as "that
            // address already has an open invitation" with nothing to say why.
            invitation.Status = HouseholdInvitationStatus.Revoked;
            invitation.RespondedAt = now;
            await _households.SaveChangesAsync(cancellationToken);

            return HouseholdResult<HouseholdResponseDto>.Conflict(
                "The person who invited you has left that household. Ask someone still in it to invite you again.");
        }

        user.HouseholdId = household.Id;
        invitation.Status = HouseholdInvitationStatus.Accepted;
        invitation.RespondedAt = now;

        // The joiner's existing records join with them. Without this the household would see
        // only what each member entered after joining, which is not what "we share our
        // finances" means to anyone.
        await _households.StampRecordsAsync(userId, household.Id, cancellationToken);

        await _households.SaveChangesAsync(cancellationToken);

        var members = await _households.GetMembersAsync(household.Id, cancellationToken);

        return HouseholdResult<HouseholdResponseDto>.Success(
            HouseholdResponseDto.FromEntity(household, members, userId));
    }
}
