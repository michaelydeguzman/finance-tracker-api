using FinanceTracker.Application.Dtos.Auth;
using MediatR;

namespace FinanceTracker.Application.Features.Auth.Commands.VerifyEmail;

/// <summary>Marks an address verified. False when the token is unusable.</summary>
public sealed record VerifyEmailCommand(TokenRequestDto Dto) : IRequest<bool>;
