using FinanceTracker.Application.Dtos.Auth;
using MediatR;

namespace FinanceTracker.Application.Features.Auth.Commands.Login;

/// <summary>Signs in with email and password. Null when the credentials do not match.</summary>
public sealed record LoginCommand(LoginRequestDto Dto) : IRequest<AuthResultDto?>;
