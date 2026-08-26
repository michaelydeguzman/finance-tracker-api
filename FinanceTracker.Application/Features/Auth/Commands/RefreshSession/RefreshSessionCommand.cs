using FinanceTracker.Application.Dtos.Auth;
using MediatR;

namespace FinanceTracker.Application.Features.Auth.Commands.RefreshSession;

/// <summary>Exchanges a refresh token for a new session. Null when the token is unusable.</summary>
public sealed record RefreshSessionCommand(TokenRequestDto Dto) : IRequest<AuthResultDto?>;
