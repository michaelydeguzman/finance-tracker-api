using FinanceTracker.Application.Services.Auth;
using MediatR;

namespace FinanceTracker.Application.Features.Auth.Commands.Register;

public sealed class RegisterCommandHandler : IRequestHandler<RegisterCommand>
{
    private readonly IAuthService _authService;

    public RegisterCommandHandler(IAuthService authService) => _authService = authService;

    public Task Handle(RegisterCommand request, CancellationToken cancellationToken) =>
        _authService.RegisterAsync(request.Dto, cancellationToken);
}
