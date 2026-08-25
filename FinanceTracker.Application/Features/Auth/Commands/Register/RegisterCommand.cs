using FinanceTracker.Application.Dtos.Auth;
using MediatR;

namespace FinanceTracker.Application.Features.Auth.Commands.Register;

/// <summary>Creates an account and emails a verification link.</summary>
public sealed record RegisterCommand(RegisterRequestDto Dto) : IRequest;
