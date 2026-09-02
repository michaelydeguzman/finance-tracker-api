using FinanceTracker.Application.Dtos.Responses;
using MediatR;

namespace FinanceTracker.Application.Features.Households.Queries.GetMyInvitations;

/// <summary>Open invitations addressed to the caller's own email address.</summary>
public sealed record GetMyInvitationsQuery : IRequest<List<HouseholdInvitationDto>>;
