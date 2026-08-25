using FinanceTracker.Application.Dtos.Auth;
using MediatR;

namespace FinanceTracker.Application.Features.Auth.Commands.RequestPasswordReset;

/// <summary>Emails a single-use password reset link, if the address has an account.</summary>
public sealed record RequestPasswordResetCommand(EmailOnlyRequestDto Dto) : IRequest;
