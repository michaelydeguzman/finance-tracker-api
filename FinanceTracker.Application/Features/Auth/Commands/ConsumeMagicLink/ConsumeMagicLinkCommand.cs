using FinanceTracker.Application.Dtos.Auth;
using MediatR;

namespace FinanceTracker.Application.Features.Auth.Commands.ConsumeMagicLink;

/// <summary>Redeems a sign-in link. Null when the token is unusable.</summary>
public sealed record ConsumeMagicLinkCommand(TokenRequestDto Dto) : IRequest<AuthResultDto?>;
