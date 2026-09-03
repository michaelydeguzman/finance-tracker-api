using MediatR;

namespace FinanceTracker.Application.Features.Households.Commands.RevokeHouseholdInvitation;

public sealed record RevokeHouseholdInvitationCommand(Guid InvitationId)
    : IRequest<HouseholdResult<object>>;
