using FinanceTracker.Application.Services.Auth;
using MediatR;

namespace FinanceTracker.Application.Features.Auth.Commands.VerifyEmail;

public sealed class VerifyEmailCommandHandler : IRequestHandler<VerifyEmailCommand, bool>
{
    private readonly IAuthService _authService;

    public VerifyEmailCommandHandler(IAuthService authService) => _authService = authService;

    public Task<bool> Handle(VerifyEmailCommand request, CancellationToken cancellationToken) =>
        _authService.VerifyEmailAsync(request.Dto, cancellationToken);
}
