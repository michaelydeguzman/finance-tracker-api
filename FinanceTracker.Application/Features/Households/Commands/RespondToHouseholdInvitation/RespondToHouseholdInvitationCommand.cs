using FinanceTracker.Application.Dtos.Responses;
using MediatR;

namespace FinanceTracker.Application.Features.Households.Commands.RespondToHouseholdInvitation;

/// <summary>
/// Accept or decline one invitation. Both answers travel the same command because they share
/// every check that matters — is it mine, is it still open — and differ only in what happens
/// once those pass.
/// </summary>
public sealed record RespondToHouseholdInvitationCommand(Guid InvitationId, bool Accept)
    : IRequest<HouseholdResult<HouseholdResponseDto>>;
