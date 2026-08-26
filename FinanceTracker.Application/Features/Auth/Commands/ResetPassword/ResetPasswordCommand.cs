using FinanceTracker.Application.Dtos.Auth;
using MediatR;

namespace FinanceTracker.Application.Features.Auth.Commands.ResetPassword;

/// <summary>Sets a new password from a reset token. False when the token is unusable.</summary>
public sealed record ResetPasswordCommand(ResetPasswordRequestDto Dto) : IRequest<bool>;
