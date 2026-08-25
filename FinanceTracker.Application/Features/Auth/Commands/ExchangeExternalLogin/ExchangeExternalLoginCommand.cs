using FinanceTracker.Application.Dtos.Auth;
using MediatR;

namespace FinanceTracker.Application.Features.Auth.Commands.ExchangeExternalLogin;

/// <summary>Turns a completed SSO sign-in into an account and a session.</summary>
public sealed record ExchangeExternalLoginCommand(ExternalLoginRequestDto Dto) : IRequest<AuthResultDto>;
