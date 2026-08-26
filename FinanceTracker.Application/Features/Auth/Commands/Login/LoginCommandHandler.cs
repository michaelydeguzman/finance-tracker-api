using FinanceTracker.Application.Services.Auth;
using MediatR;
using FinanceTracker.Application.Dtos.Auth;
namespace FinanceTracker.Application.Features.Auth.Commands.Login;

public sealed class LoginCommandHandler : IRequestHandler<LoginCommand, AuthResultDto?>
{
    private readonly IAuthService _authService;

    public LoginCommandHandler(IAuthService authService) => _authService = authService;

    public Task<AuthResultDto?> Handle(LoginCommand request, CancellationToken cancellationToken) =>
        _authService.LoginAsync(request.Dto, cancellationToken);
}
