using FinanceTracker.Application.Dtos.Responses;
using FinanceTracker.Application.Options;
using FinanceTracker.Application.Services.Email;
using FinanceTracker.Application.Services.Households;
using FinanceTracker.Domain.Entities;
using FinanceTracker.Domain.Repositories;
using FinanceTracker.Domain.Services;
using MediatR;
using Microsoft.Extensions.Options;

namespace FinanceTracker.Application.Features.Households.Commands.InviteHouseholdMember;

public sealed class InviteHouseholdMemberCommandHandler
    : IRequestHandler<InviteHouseholdMemberCommand, HouseholdResult<HouseholdInvitationDto>>
{
    private readonly IHouseholdRepository _households;
    private readonly IUserRepository _users;
    private readonly IEmailSender _email;
    private readonly ICurrentUserAccessor _currentUser;
    private readonly AuthOptions _authOptions;

    public InviteHouseholdMemberCommandHandler(
        IHouseholdRepository households,
        IUserRepository users,
        IEmailSender email,
        ICurrentUserAccessor currentUser,
        IOptions<AuthOptions> authOptions)
    {
        _households = households;
        _users = users;
        _email = email;
        _currentUser = currentUser;
        _authOptions = authOptions.Value;
    }

    public async Task<HouseholdResult<HouseholdInvitationDto>> Handle(
        InviteHouseholdMemberCommand request,
        CancellationToken cancellationToken)
    {
        var userId = _currentUser.RequireUserId();

        if (!HouseholdAddress.LooksLikeAnEmail(request.Dto.Email))
            return HouseholdResult<HouseholdInvitationDto>.Invalid("A valid email address is required.");

        var invitedEmail = HouseholdAddress.Normalize(request.Dto.Email);

        var household = await _households.GetForUserAsync(userId, cancellationToken);

        if (household is null)
            return HouseholdResult<HouseholdInvitationDto>.NotFound("You are not in a household.");

        if (household.OwnerUserId != userId)
            return HouseholdResult<HouseholdInvitationDto>.Forbidden("Only the household owner can invite people.");

        var inviter = await _users.GetByIdAsync(userId, cancellationToken);

        if (inviter is null)
            return HouseholdResult<HouseholdInvitationDto>.NotFound("Your account could not be found.");

        if (invitedEmail == inviter.Email)
            return HouseholdResult<HouseholdInvitationDto>.Conflict("You are already in this household.");

        var now = DateTime.UtcNow;

        // Whether the address already belongs to a member is the one membership question this
        // may answer, because the caller can already see every member by name. It deliberately
        // stops there: an address that has no account, or one whose account is in somebody
        // else's household, produces exactly the same invitation as any other, so this cannot
        // be used to find out who has registered.
        var existing = await _users.GetByEmailAsync(invitedEmail, cancellationToken);

        if (existing is not null && existing.HouseholdId == household.Id)
            return HouseholdResult<HouseholdInvitationDto>.Conflict("That person is already in this household.");

        if (await _households.HasOpenInvitationAsync(household.Id, invitedEmail, now, cancellationToken))
            return HouseholdResult<HouseholdInvitationDto>.Conflict("That address already has an open invitation.");

        var invitation = new HouseholdInvitation
        {
            Id = Guid.NewGuid(),
            HouseholdId = household.Id,
            InvitedEmail = invitedEmail,
            InvitedByUserId = userId,
            Status = HouseholdInvitationStatus.Pending,
            CreatedAt = now,
            ExpiresAt = now.AddDays(_authOptions.HouseholdInvitationDays)
        };

        await _households.AddInvitationAsync(invitation, cancellationToken);
        await _households.SaveChangesAsync(cancellationToken);

        // Sent after the invitation is committed, and through NonFatalEmailSender, so a dead
        // mail server does not turn a saved invitation into a failed request. The invitee can
        // still find it on their households page.
        await _email.SendAsync(
            HouseholdEmailFactory.Invitation(
                invitedEmail,
                household.Name,
                inviter.DisplayName ?? inviter.Email,
                $"{_authOptions.AppBaseUrl.TrimEnd('/')}/households"),
            cancellationToken);

        return HouseholdResult<HouseholdInvitationDto>.Success(
            HouseholdInvitationDto.FromEntity(invitation, household.Name));
    }
}
