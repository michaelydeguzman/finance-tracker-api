using FinanceTracker.Application.Services.Auth;
using MediatR;

namespace FinanceTracker.Application.Features.Auth.Commands.ResetPassword;

public sealed class ResetPasswordCommandHandler : IRequestHandler<ResetPasswordCommand, bool>
{
    private readonly IAuthService _authService;

    public ResetPasswordCommandHandler(IAuthService authService) => _authService = authService;

    public Task<bool> Handle(ResetPasswordCommand request, CancellationToken cancellationToken) =>
        _authService.ResetPasswordAsync(request.Dto, cancellationToken);
}
